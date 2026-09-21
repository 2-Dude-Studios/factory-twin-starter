---
description: Run EditMode tests headless and summarise failures
---
Run the Unity EditMode tests for this project headless and summarise the result.

Windows command (adjust the Editor path if 6000.3 lives elsewhere):
```
"C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults tools/test-results.xml -logFile tools/test.log
```
If the `unity` CLI is available, prefer `unity test --platform EditMode`.

Then read `tools/test-results.xml`, list failing tests with their message and the file/line, and propose the fix for the first failure only. Do not edit tests to make them pass.

$ARGUMENTS
