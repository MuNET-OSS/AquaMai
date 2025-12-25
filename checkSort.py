#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
检查 configSort.yaml 和 AquaMai.zh.toml 的一致性

检查内容：
1. AquaMai.zh.toml 中的所有 Sections 是否都在 configSort.yaml 中存在
2. configSort.yaml 中"社区功能"分类的内容是否同时存在于其他分类中
"""

import sys
from pathlib import Path
import yaml


def load_toml_sections(toml_path: Path) -> set[str]:
    """从 TOML 文件中提取所有 Section 名称（通过解析 [Section] 标记）"""
    sections = set()

    with open(toml_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            # 匹配 [Section] 或 #[Section] 格式
            if line.startswith("[") or line.startswith("#["):
                # 去掉注释符号
                if line.startswith("#"):
                    line = line[1:]
                # 提取 Section 名称
                if line.startswith("[") and line.endswith("]"):
                    section = line[1:-1]
                    sections.add(section)

    return sections


def load_yaml_sections(yaml_path: Path) -> dict[str, list[str]]:
    """从 YAML 文件中提取所有分类及其包含的 Sections"""
    with open(yaml_path, "r", encoding="utf-8") as f:
        data = yaml.safe_load(f)

    return data


def main():
    # 文件路径
    toml_path = Path("Output/AquaMai.zh.toml")
    yaml_path = Path("AquaMai/configSort.yaml")

    if not toml_path.exists():
        print(f"[错误] 找不到文件 {toml_path}")
        return 1

    if not yaml_path.exists():
        print(f"[错误] 找不到文件 {yaml_path}")
        return 1

    # 加载数据
    toml_sections = load_toml_sections(toml_path)
    yaml_categories = load_yaml_sections(yaml_path)

    # 收集 YAML 中所有的 sections
    yaml_all_sections = set()
    for sections in yaml_categories.values():
        yaml_all_sections.update(sections)

    # 检查 1: TOML 中的所有 Sections 是否都在 YAML 中存在
    missing_in_yaml = toml_sections - yaml_all_sections

    if missing_in_yaml:
        print("[失败] 以下 Sections 在 AquaMai.zh.toml 中存在，但不在 configSort.yaml 中：")
        for section in sorted(missing_in_yaml):
            print(f"  - {section}")
        return 1

    # 检查 2: "社区功能" 中的内容是否都存在于其他分类中
    community_sections = set(yaml_categories.get("社区功能", []))
    other_sections = set()

    for category, sections in yaml_categories.items():
        if category != "社区功能":
            other_sections.update(sections)

    missing_in_other = community_sections - other_sections

    if missing_in_other:
        print("[失败] 以下 Sections 在\"社区功能\"中存在，但不在其他分类中：")
        for section in sorted(missing_in_other):
            print(f"  - {section}")
        return 1

    # 所有检查通过
    print("[通过] 检查通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
