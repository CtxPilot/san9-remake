import json
import tempfile
import unittest
from pathlib import Path

from validate_core_domain_contracts import validate_contracts


class ValidateCoreDomainContractsTests(unittest.TestCase):
    def make_fixture(self, root: Path) -> tuple[dict[str, object], Path, Path]:
        behavior_dir = root / "specs" / "behavior"
        decision_dir = root / "specs" / "decisions"
        source_dir = root / "src" / "Core"
        behavior_dir.mkdir(parents=True)
        decision_dir.mkdir(parents=True)
        source_dir.mkdir(parents=True)
        (source_dir / "Entry.cs").write_text("namespace Core;", encoding="utf-8")
        (behavior_dir / "BEH-SIMULATION-0001-test.md").write_text(
            "# test\n\n- 状态：已确认\n- 关联决策：[DEC-SIM-0001](../decisions/x.md)\n"
            "\n## 重制行为\n\n行为。\n\n## 状态与不变量\n\n不变量。\n"
            "\n## 验收标准\n\n- [ ] 验收。\n\n## 未决问题\n\n无。\n",
            encoding="utf-8",
        )
        (decision_dir / "DEC-SIM-0001-test.md").write_text(
            "# decision\n\n- 状态：已确认\n", encoding="utf-8"
        )
        contracts = {
            "schema_version": 1,
            "status": "confirmed",
            "domains": [{
                "id": "simulation",
                "classification": "core",
                "behavior_specs": ["BEH-SIMULATION-0001"],
                "objects": ["Clock"],
                "states": ["stable"],
                "inputs": ["command"],
                "outputs": ["snapshot"],
                "invariants": ["one clock"],
                "acceptance_gates": ["advance"],
                "depends_on": [],
                "implementation": {
                    "project": "Core",
                    "target_namespace": "Core",
                    "target_path": "src/Core",
                    "entry_types": ["Entry"],
                    "existing_anchors": ["src/Core/Entry.cs"],
                },
            }],
            "decision_gates": [{
                "id": "determinism",
                "kind": "system-redesign",
                "behavior_specs": ["BEH-SIMULATION-0001"],
                "decision": "DEC-SIM-0001",
                "disposition": "stable order",
            }],
            "unresolved_conflicts": [],
        }
        return contracts, behavior_dir, decision_dir

    def test_accepts_complete_contract(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            result = validate_contracts(
                contracts, behavior_dir, decision_dir, root, {"simulation"}
            )
        self.assertTrue(result["validation"]["valid"])
        self.assertEqual(result["behavior_spec_count"], 1)
        self.assertEqual(result["acceptance_criteria_count"], 1)

    def test_rejects_omission_leak_missing_anchor_and_conflict(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            extra = behavior_dir / "BEH-EVENT-0001-extra.md"
            extra.write_text("# extra\n\n- 状态：已确认\n", encoding="utf-8")
            domain = contracts["domains"][0]
            domain["objects"] = ["RE-SAMPLE at 0x401000"]
            domain["implementation"]["existing_anchors"] = ["src/Core/Missing.cs"]
            contracts["unresolved_conflicts"] = ["open"]
            result = validate_contracts(
                contracts, behavior_dir, decision_dir, root, {"simulation"}
            )
        issues = result["validation"]["issues"]
        self.assertFalse(result["validation"]["valid"])
        self.assertIn("unclassified_behavior_specs", issues)
        self.assertIn("research_detail_leak", issues)
        self.assertIn("missing_existing_anchor", issues)
        self.assertIn("unresolved_conflicts", issues)

    def test_rejects_dependency_cycle(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            contracts["domains"].append({
                **json.loads(json.dumps(contracts["domains"][0])),
                "id": "event",
                "behavior_specs": [],
                "depends_on": ["simulation"],
            })
            contracts["domains"][0]["depends_on"] = ["event"]
            result = validate_contracts(
                contracts, behavior_dir, decision_dir, root, {"simulation", "event"}
            )
        self.assertIn("dependency_cycle", result["validation"]["issues"])

    def test_non_contract_statuses_are_reported_but_cannot_enter_contract(self):
        for status in ("草案", "验证中", "已废弃"):
            with self.subTest(status=status), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                contracts, behavior_dir, decision_dir = self.make_fixture(root)
                spec_id = "BEH-EVENT-0001"
                (behavior_dir / f"{spec_id}-draft.md").write_text(
                    f"# draft\n\n- 状态：{status}\n", encoding="utf-8"
                )
                result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
                self.assertTrue(result["validation"]["valid"])
                self.assertEqual(result["non_contract_behavior_specs"], {spec_id: status})
                self.assertEqual(result["confirmed_behavior_spec_count"], 1)
                contracts["decision_gates"][0]["behavior_specs"].append(spec_id)
                result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
                self.assertIn("decision_gate_unclassified_spec", result["validation"]["issues"])
                contracts["domains"][0]["behavior_specs"].append(spec_id)
                result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
                self.assertIn("unconfirmed_behavior_spec", result["validation"]["issues"])

    def test_missing_or_unknown_status_is_not_silently_excluded(self):
        for metadata in ("", "- 状态：采用", "- 状态：已确认错误"):
            with self.subTest(metadata=metadata), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                contracts, behavior_dir, decision_dir = self.make_fixture(root)
                (behavior_dir / "BEH-EVENT-0001-extra.md").write_text(
                    f"# extra\n\n{metadata}\n", encoding="utf-8"
                )
                result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
                self.assertIn("invalid_behavior_status", result["validation"]["issues"])

    def test_accepts_topic_sources_with_prose_and_table_acceptance(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            spec = behavior_dir / "BEH-SIMULATION-0001-test.md"
            spec.write_text("# 专题入口\n\n- 状态：已确认\n", encoding="utf-8")
            # A behavior needs no separate DEC unless a declared gate requires it.
            contracts["decision_gates"] = []
            topic = root / "specs" / "simulation.md"
            topic.write_text(
                "# 模拟专题\n\n## 推进\n\n稳定边界发布下一日。\n"
                "\n## 所有权\n\n只读查询不得改变世界。\n"
                "\n## 验收\n\n相同初态和操作序列产生相同结果。\n\n"
                "| 输入 | 结果 |\n| --- | --- |\n| 非法命令 | 无状态变化 |\n",
                encoding="utf-8",
            )
            contracts["specification_sources"] = {"BEH-SIMULATION-0001": {
                "behavior": "specs/simulation.md#推进",
                "invariants": "specs/simulation.md#所有权",
                "acceptance": "specs/simulation.md#验收",
            }}
            result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
        self.assertTrue(result["validation"]["valid"], result["validation"])
        self.assertEqual(result["acceptance_criteria_count"], 2)

    def test_rejects_broken_unsafe_and_empty_topic_sources(self):
        cases = [
            (None, "invalid_specification_source"),
            ("specs/missing.md", "missing_specification_source"),
            ("specs/topic.md#不存在", "missing_specification_content"),
            ("specs/topic.md#空章节", "missing_specification_content"),
            ("specs/topic.md#样例章节", "missing_specification_content"),
            ("specs/../outside.md", "invalid_specification_source"),
            ("specs/empty.md", "missing_specification_content"),
        ]
        for source, error in cases:
            with self.subTest(source=source), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                contracts, behavior_dir, decision_dir = self.make_fixture(root)
                (root / "outside.md").write_text("outside", encoding="utf-8")
                (root / "specs" / "topic.md").write_text(
                    "# 主题\n\n```markdown\n## 样例章节\n样例不是正文。\n```\n"
                    "\n## 空章节\n\n### 只有标题\n\n## 正文\n有效内容。\n",
                    encoding="utf-8",
                )
                (root / "specs" / "empty.md").write_text(
                    "# 空主题\n\n- 状态：已确认\n\n## 验收\n\n| 条件 | 结果 |\n| --- | --- |\n",
                    encoding="utf-8",
                )
                contracts["specification_sources"] = {"BEH-SIMULATION-0001": {"acceptance": source}}
                result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
                self.assertIn(error, result["validation"]["issues"])

    def test_rejects_unknown_source_spec_and_role(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            contracts["specification_sources"] = {
                "BEH-MISSING-0001": {},
                "BEH-SIMULATION-0001": {"accceptance": "specs/topic.md"},
            }
            result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
        self.assertIn("unknown_source_spec", result["validation"]["issues"])
        self.assertIn("invalid_specification_sources", result["validation"]["issues"])

    def test_absent_question_section_is_not_a_contract_error(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            spec = behavior_dir / "BEH-SIMULATION-0001-test.md"
            spec.write_text(spec.read_text(encoding="utf-8").split("## 未决问题")[0], encoding="utf-8")
            result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
        self.assertTrue(result["validation"]["valid"])

    def test_retired_decision_cannot_remain_an_active_gate(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts, behavior_dir, decision_dir = self.make_fixture(root)
            decision = decision_dir / "DEC-SIM-0001-test.md"
            decision.write_text("# 历史决策\n\n- 状态：已废弃\n", encoding="utf-8")
            result = validate_contracts(contracts, behavior_dir, decision_dir, root, {"simulation"})
        self.assertIn("unconfirmed_decision", result["validation"]["issues"])
        self.assertIn("behavior_unconfirmed_decision", result["validation"]["issues"])


if __name__ == "__main__":
    unittest.main()
