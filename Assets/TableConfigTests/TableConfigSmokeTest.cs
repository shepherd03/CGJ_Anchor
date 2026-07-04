using System;
using System.Collections.Generic;
using System.Text;
using Anchor.Config;
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

using BuffRow = Anchor.Config.game.buff;
using EventRow = Anchor.Config.game.gameEvent;

namespace Anchor.TableConfigTests
{
    public sealed class TableConfigSmokeTest : MonoBehaviour
    {
        [SerializeField] private bool mRunOnStart = true;
        [SerializeField] private bool mLogRows = true;
        [SerializeField] private bool mThrowOnFailure;

        private void Start()
        {
            if (!mRunOnStart)
                return;

            var success = RunSmokeTest(mLogRows);
            if (!success && mThrowOnFailure)
                throw new InvalidOperationException("Table config smoke test failed. Check Console errors.");
        }

        [ContextMenu("Run Table Config Smoke Test")]
        private void RunFromContextMenu()
        {
            RunSmokeTest(mLogRows);
        }

        public static bool RunSmokeTest(bool logRows = true)
        {
            var errors = new List<string>();
            var output = new StringBuilder();
            output.AppendLine("[TableConfigSmokeTest] Begin");

            try
            {
                EnsureResKitProvider();
                GameConfigs.Unload();

                var tables = GameConfigs.Load(forceReload: true);
                ValidateEvents(tables.TbgameEvent.DataList, errors, output, logRows);
                ValidateBuffs(tables.Tbbuff.DataList, errors, output, logRows);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                Debug.LogException(exception);
            }

            if (errors.Count == 0)
            {
                output.AppendLine("[TableConfigSmokeTest] PASS");
                Debug.Log(output.ToString());
                return true;
            }

            output.AppendLine("[TableConfigSmokeTest] FAIL");
            Debug.Log(output.ToString());
            foreach (var error in errors)
            {
                Debug.LogError("[TableConfigSmokeTest] " + error);
            }

            return false;
        }

        private static void EnsureResKitProvider()
        {
            if (ResKit.GetProvider() == null)
                ResKit.SetProvider(new UnityResourceProvider());
        }

        private static void ValidateEvents(
            IReadOnlyList<EventRow> rows,
            ICollection<string> errors,
            StringBuilder output,
            bool logRows)
        {
            output.AppendLine($"Events: {rows.Count}");
            if (rows.Count == 0)
                errors.Add("game.TbgameEvent has no data rows.");

            var ids = new HashSet<int>();
            foreach (var row in rows)
            {
                CheckId("Event", row.Id, ids, errors);
                CheckRequired("Event", row.Id, "title", row.Title, errors);
                CheckRequired("Event", row.Id, "content", row.Content, errors);
                CheckCost("Event", row.Id, row.Cost, errors);
                CheckRange("Event", row.Id, "ratio", row.Ratio, 0f, 1f, errors);
                CheckFinite("Event", row.Id, "y1", row.Y1, errors);
                CheckFinite("Event", row.Id, "y2", row.Y2, errors);
                CheckFinite("Event", row.Id, "y3", row.Y3, errors);
                CheckFinite("Event", row.Id, "y4", row.Y4, errors);
                CheckFinite("Event", row.Id, "n1", row.N1, errors);
                CheckFinite("Event", row.Id, "n2", row.N2, errors);
                CheckFinite("Event", row.Id, "n3", row.N3, errors);
                CheckFinite("Event", row.Id, "n4", row.N4, errors);
                CheckFinite("Event", row.Id, "t1", row.T1, errors);
                CheckFinite("Event", row.Id, "t2", row.T2, errors);
                CheckFinite("Event", row.Id, "t3", row.T3, errors);
                CheckFinite("Event", row.Id, "t4", row.T4, errors);

                if (logRows)
                {
                    output.AppendLine(
                        $"  Event[{row.Id}] title={row.Title}, type={row.Type}, ratio={row.Ratio}, cost={row.Cost}, y=({row.Y1},{row.Y2},{row.Y3},{row.Y4}), n=({row.N1},{row.N2},{row.N3},{row.N4}), t=({row.T1},{row.T2},{row.T3},{row.T4})");
                }
            }
        }

        private static void ValidateBuffs(
            IReadOnlyList<BuffRow> rows,
            ICollection<string> errors,
            StringBuilder output,
            bool logRows)
        {
            output.AppendLine($"Buffs: {rows.Count}");
            if (rows.Count == 0)
                errors.Add("game.Tbbuff has no data rows.");

            var ids = new HashSet<int>();
            foreach (var row in rows)
            {
                CheckId("Buff", row.Id, ids, errors);
                CheckRequired("Buff", row.Id, "title", row.Title, errors);
                CheckRequired("Buff", row.Id, "content", row.Content, errors);
                CheckCost("Buff", row.Id, row.Cost, errors);
                CheckFinite("Buff", row.Id, "c1", row.C1, errors);
                CheckFinite("Buff", row.Id, "c2", row.C2, errors);
                CheckFinite("Buff", row.Id, "c3", row.C3, errors);
                CheckFinite("Buff", row.Id, "c11", row.C11, errors);
                CheckFinite("Buff", row.Id, "t11", row.T11, errors);
                CheckFinite("Buff", row.Id, "c22", row.C22, errors);
                CheckFinite("Buff", row.Id, "t22", row.T22, errors);
                CheckFinite("Buff", row.Id, "c33", row.C33, errors);
                CheckFinite("Buff", row.Id, "t33", row.T33, errors);

                if (logRows)
                {
                    output.AppendLine(
                        $"  Buff[{row.Id}] title={row.Title}, type={row.Type}, cost={row.Cost}, c=({row.C1},{row.C2},{row.C3},{row.C11},{row.C22},{row.C33}), t=({row.T11},{row.T22},{row.T33})");
                }
            }
        }

        private static void CheckId(string table, int id, ISet<int> ids, ICollection<string> errors)
        {
            if (id <= 0)
                errors.Add($"{table} has invalid id: {id}.");

            if (!ids.Add(id))
                errors.Add($"{table} has duplicate id: {id}.");
        }

        private static void CheckRequired(
            string table,
            int id,
            string field,
            string value,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add($"{table}[{id}] field '{field}' is empty.");
        }

        private static void CheckCost(string table, int id, int cost, ICollection<string> errors)
        {
            if (cost < 0)
                errors.Add($"{table}[{id}] cost is negative: {cost}.");
        }

        private static void CheckRange(
            string table,
            int id,
            string field,
            float value,
            float min,
            float max,
            ICollection<string> errors)
        {
            CheckFinite(table, id, field, value, errors);
            if (value < min || value > max)
                errors.Add($"{table}[{id}] field '{field}' is out of range [{min}, {max}]: {value}.");
        }

        private static void CheckFinite(string table, int id, string field, float value, ICollection<string> errors)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                errors.Add($"{table}[{id}] field '{field}' is not finite: {value}.");
        }
    }
}
