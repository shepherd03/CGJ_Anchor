#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Anchor.TableConfigTests
{
    public static class TableConfigSmokeTestEditorRunner
    {
        [MenuItem("Anchor/Table Config/Run Smoke Test")]
        public static void RunFromMenu()
        {
            RunOrThrow();
        }

        public static void RunBatch()
        {
            var success = TableConfigSmokeTest.RunSmokeTest(logRows: true);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(success ? 0 : 1);
                return;
            }

            if (!success)
                throw new InvalidOperationException("Table config smoke test failed. Check Console errors.");
        }

        private static void RunOrThrow()
        {
            if (!TableConfigSmokeTest.RunSmokeTest(logRows: true))
                throw new InvalidOperationException("Table config smoke test failed. Check Console errors.");
        }
    }
}
#endif
