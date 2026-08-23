#!/usr/bin/env python3
"""
Directory.Packages.props で管理している NuGet パッケージの更新を検出し、
メジャーバージョンアップはパッケージごとに 1 PR、マイナー・パッチバージョンアップは
まとめて 1 PR を作成する。

GitHub Actions のスケジュール実行（.github/workflows/dependency-update.yml）から呼び出される。
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PROPS_FILE = REPO_ROOT / "Directory.Packages.props"
SOLUTION = "SQLForge.sln"
BASE_BRANCH = "main"


@dataclass
class PackageUpdate:
    id: str
    current: str
    latest: str
    bump: str  # "major" | "minor" | "patch"


def run(cmd: list[str], **kwargs) -> subprocess.CompletedProcess:
    print(f"+ {' '.join(cmd)}", file=sys.stderr)
    return subprocess.run(cmd, cwd=REPO_ROOT, check=True, text=True, **kwargs)


def parse_version(version: str) -> tuple[int, int, int] | None:
    match = re.match(r"^(\d+)\.(\d+)\.(\d+)", version)
    if not match:
        return None
    return tuple(int(part) for part in match.groups())  # type: ignore[return-value]


def classify(current: str, latest: str) -> str | None:
    current_v = parse_version(current)
    latest_v = parse_version(latest)
    if current_v is None or latest_v is None:
        print(f"警告: バージョン形式を解釈できないためスキップします: {current} -> {latest}", file=sys.stderr)
        return None
    if latest_v <= current_v:
        return None
    if latest_v[0] > current_v[0]:
        return "major"
    return "minor_patch"


def collect_updates() -> list[PackageUpdate]:
    run(["dotnet", "restore", SOLUTION])
    result = run(
        ["dotnet", "list", SOLUTION, "package", "--outdated", "--format", "json"],
        capture_output=True,
    )
    data = json.loads(result.stdout)

    updates: dict[str, PackageUpdate] = {}
    for project in data.get("projects", []):
        for framework in project.get("frameworks", []):
            for pkg in framework.get("topLevelPackages", []):
                pkg_id = pkg["id"]
                current = pkg["resolvedVersion"]
                latest = pkg["latestVersion"]
                bump = classify(current, latest)
                if bump is None:
                    continue
                existing = updates.get(pkg_id)
                if existing is not None and existing.latest == latest and existing.current == current:
                    continue
                updates[pkg_id] = PackageUpdate(id=pkg_id, current=current, latest=latest, bump=bump)

    return sorted(updates.values(), key=lambda u: u.id)


def slugify(package_id: str) -> str:
    return re.sub(r"[^a-zA-Z0-9._-]", "-", package_id).lower()


def branch_exists_remotely(branch: str) -> bool:
    result = subprocess.run(
        ["git", "ls-remote", "--exit-code", "--heads", "origin", branch],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
    )
    return result.returncode == 0


def update_props_file(package_ids_to_versions: dict[str, str]) -> None:
    text = PROPS_FILE.read_text(encoding="utf-8")
    for pkg_id, new_version in package_ids_to_versions.items():
        pattern = re.compile(
            r'(<PackageVersion\s+Include="' + re.escape(pkg_id) + r'"\s+Version=")[^"]+(")'
        )
        new_text, count = pattern.subn(rf"\g<1>{new_version}\g<2>", text)
        if count != 1:
            raise RuntimeError(f"Directory.Packages.props 内で {pkg_id} の置換に失敗しました（{count} 件一致）")
        text = new_text
    PROPS_FILE.write_text(text, encoding="utf-8")


def create_pr(branch: str, title: str, body: str, dry_run: bool) -> None:
    if dry_run:
        print(f"[dry-run] PR作成をスキップ: branch={branch}, title={title}")
        return
    run(["gh", "pr", "create", "--base", BASE_BRANCH, "--head", branch, "--title", title, "--body", body])


def commit_and_push(branch: str, message: str, dry_run: bool) -> bool:
    status = subprocess.run(
        ["git", "status", "--porcelain", str(PROPS_FILE)],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=True,
    )
    if not status.stdout.strip():
        print(f"変更がないためコミットをスキップ: {branch}", file=sys.stderr)
        return False

    run(["git", "add", str(PROPS_FILE)])
    run(["git", "commit", "-m", message])
    if dry_run:
        print(f"[dry-run] push をスキップ: {branch}")
    else:
        run(["git", "push", "origin", branch])
    return True


def process_major_update(update: PackageUpdate, dry_run: bool) -> None:
    branch = f"deps/major/{slugify(update.id)}-{update.latest}"
    if branch_exists_remotely(branch):
        print(f"既に提案済みのためスキップ: {branch}", file=sys.stderr)
        return

    run(["git", "checkout", BASE_BRANCH])
    run(["git", "checkout", "-b", branch])
    try:
        update_props_file({update.id: update.latest})
        message = f"chore(deps): {update.id} を {update.current} から {update.latest} に更新 (メジャー)"
        if commit_and_push(branch, message, dry_run):
            title = f"chore(deps): {update.id} {update.current} -> {update.latest} (メジャーアップデート)"
            body = (
                f"`{update.id}` をメジャーバージョン {update.current} から {update.latest} に更新します。\n\n"
                "メジャーバージョンアップのため破壊的変更が含まれる可能性があります。"
                "変更履歴を確認のうえマージしてください。\n\n"
                "このPRは週次の依存関係更新チェック（GitHub Actions）により自動作成されました。"
            )
            create_pr(branch, title, body, dry_run)
    finally:
        run(["git", "checkout", BASE_BRANCH])


def process_minor_patch_updates(updates: list[PackageUpdate], dry_run: bool) -> None:
    if not updates:
        return

    branch = "deps/minor-patch-updates"
    if branch_exists_remotely(branch):
        print(f"既に提案済みのためスキップ: {branch}", file=sys.stderr)
        return

    run(["git", "checkout", BASE_BRANCH])
    run(["git", "checkout", "-b", branch])
    try:
        update_props_file({u.id: u.latest for u in updates})
        summary = "、".join(f"{u.id} {u.current}→{u.latest}" for u in updates)
        message = f"chore(deps): {len(updates)} 件の依存パッケージを更新 (マイナー・パッチ)\n\n{summary}"
        if commit_and_push(branch, message, dry_run):
            title = f"chore(deps): {len(updates)} 件の依存パッケージを更新 (マイナー・パッチ)"
            body_lines = [
                "以下のパッケージをマイナー・パッチバージョンで更新します。",
                "",
                "| パッケージ | 現在 | 更新後 |",
                "| --- | --- | --- |",
            ]
            for u in updates:
                body_lines.append(f"| {u.id} | {u.current} | {u.latest} |")
            body_lines.append("")
            body_lines.append("このPRは週次の依存関係更新チェック（GitHub Actions）により自動作成されました。")
            create_pr(branch, title, "\n".join(body_lines), dry_run)
    finally:
        run(["git", "checkout", BASE_BRANCH])


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true", help="変更のpush・PR作成を行わず、検出結果のみ表示する")
    args = parser.parse_args()

    updates = collect_updates()
    if not updates:
        print("更新可能なパッケージはありませんでした。")
        return

    majors = [u for u in updates if u.bump == "major"]
    minor_patches = [u for u in updates if u.bump == "minor_patch"]

    print("検出した更新:")
    for u in updates:
        print(f"  [{u.bump}] {u.id}: {u.current} -> {u.latest}")

    for update in majors:
        process_major_update(update, args.dry_run)

    process_minor_patch_updates(minor_patches, args.dry_run)


if __name__ == "__main__":
    main()
