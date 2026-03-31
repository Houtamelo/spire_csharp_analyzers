# SPIRE009 [EnforceExhaustiveness] End-to-End Coverage Matrix

Tests that the ExhaustivenessAnalyzer fires SPIRE009 for non-DU types with [EnforceExhaustiveness].
All cases go in `tests/Houtamelo.Spire.SourceGenerators.Tests/Exhaustiveness/cases/`.

---

## Category A: Abstract class hierarchy (switch expression)

### should_fail

| File Name | Description |
|-----------|-------------|
| `EE_Class_MissingOne` | Abstract class with 3 sealed subtypes, switch expression missing one |
| `EE_Class_MissingTwo` | Abstract class with 3 sealed subtypes, switch expression covers only one |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_EE_Class_AllCovered` | Abstract class with 3 sealed subtypes, all covered via type patterns |
| `Pass_EE_Class_Wildcard` | Abstract class, wildcard arm covers all — no diagnostic |

---

## Category B: Interface hierarchy (switch expression)

### should_fail

| File Name | Description |
|-----------|-------------|
| `EE_Interface_MissingOne` | Interface with 3 sealed implementors, switch expression missing one |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_EE_Interface_AllCovered` | Interface with 3 sealed implementors, all covered |

---

## Category C: Switch statement

### should_fail

| File Name | Description |
|-----------|-------------|
| `EE_Class_SwitchStmt_Missing` | Abstract class, switch statement missing one subtype |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_EE_Class_SwitchStmt_Full` | Abstract class, switch statement all subtypes covered |

---

## Category D: Deep hierarchy / intermediate abstract classes

### should_fail

| File Name | Description |
|-----------|-------------|
| `EE_DeepHierarchy_MissingLeaf` | Abstract base → abstract intermediate → sealed leaves; switch missing one leaf |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_EE_DeepHierarchy_AllLeaves` | Deep hierarchy, all leaf types covered |

---

## Category E: Edge cases

### should_fail

| File Name | Description |
|-----------|-------------|
| `EE_Class_WhenGuard` | Abstract class switch with `when` guard on one arm — that variant not fully covered |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_EE_Class_OrPattern` | Abstract class, all subtypes covered via `or` pattern |
| `Pass_EE_NoAttribute_NoError` | Plain abstract class without [EnforceExhaustiveness] — no SPIRE009 even if switch is incomplete |
