# RE-SAN9PK-CHEAT-0001：新武将编辑器妖术 20 次秘籍

- 状态：已确认
- 样本：`SRC-SAN9PK-TC-101`（`San9PK.exe`）
- 项目主数据库：`research/analysis/San9PK-TC-101.i64`
- 证据类型：IDA 静态反编译与指令回读

## 结论

规则、证据地址、操作配方边界及新增创建确认消费者统一见[新武将权威研究RE-0202](RE-SAN9PK-0202.md#四维兵法预算与秘籍)。本页自2026-11-01起只保留旧编号和原标注清单，不再维护重复规则；原“已确认”仅指已定位的静态链，不表示完整创建流程已经验收。

## IDA 同步

已在项目主数据库中写入以下名称与 `RE-SAN9PK-CHEAT-0001` 证据注释：

`new_person_tactic_cheat_flag`、`edit_person_ability_with_dynamic_max`、`calculate_new_person_tactic_points`、`render_new_person_tactic_points`、`activate_new_person_tactic_cheat`、`finalize_new_person_record`。

仅修改 IDA 数据库元数据，程序字节未修改。
