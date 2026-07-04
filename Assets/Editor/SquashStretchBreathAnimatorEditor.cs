#if UNITY_EDITOR
using Anchor.Animations;
using UnityEditor;
using UnityEngine;

namespace Anchor.Animations.Editor
{
    /// <summary>
    /// 给 SquashStretchBreathAnimator 的 Inspector 增加运行时立即应用参数按钮。
    /// </summary>
    [CustomEditor(typeof(SquashStretchBreathAnimator))]
    [CanEditMultipleObjects]
    public sealed class SquashStretchBreathAnimatorEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 绘制默认 Inspector，并追加运行时应用参数按钮。
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // 只有 Play Mode 中才需要重建 DOTween 序列，编辑态按钮禁用避免误解。
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("立即应用当前参数"))
                {
                    ApplyRuntimeSettingsToTargets();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后点击按钮，会立即重建 DOTween 序列并应用当前 Inspector 参数。", MessageType.Info);
            }
        }

        /// <summary>
        /// 对当前选中的所有动画组件应用 Inspector 参数。
        /// </summary>
        private void ApplyRuntimeSettingsToTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is SquashStretchBreathAnimator animator)
                {
                    animator.ApplyRuntimeSettings();
                }
            }
        }
    }
}
#endif
