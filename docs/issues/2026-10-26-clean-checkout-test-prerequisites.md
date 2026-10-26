# Existing cold-checkout integration prerequisites

An isolated checkout exposed 21 DevTools test failures before prerequisite
preparation. The product/source-generator tests passed. These failures are
outside the seven approved correctness fixes and were resolved for validation
without changing or skipping assertions.

Several customer-workflow tests require the Debug Spire.Tool CLI and Debug
runtime/analyzer outputs even during a Release solution test. Behavioral runner
tests require the Release UnityTestDependencies output. Two asmdef parser tests
read actual Unity-generated project and Bee response files from
`tmp/unity-asmdefs`, which a new checkout does not contain.

Validation prepared Debug Spire.Tool, Debug Houtamelo.Spire, and Release
UnityTestDependencies through real builds. It copied 35 real Unity-generated
metadata files from the existing validated fixture, preserving their bytes and
recording source/target SHA-256 hashes. The complete suite then passed: 2,211
tests, zero skipped. Evidence is under
`tmp/correctness-fixes-20261026/batch5-prerequisite-*.log`,
`validation-native-fixture-inputs.json`, and `batch5-inliner-isolated-ready/`.

A follow-up should either prepare these prerequisites through a shared fixture
or make the documented integration entry point perform that preparation. Keep
the real Unity metadata checks and the missing-dependency failure cases; do not
replace them with synthetic responses or blanket skips.
