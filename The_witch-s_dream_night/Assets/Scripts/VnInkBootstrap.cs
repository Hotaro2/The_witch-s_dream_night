using System.Collections;
using UnityEngine;

namespace VN
{
    public sealed class VNInkBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset compiledInkJson;
        [SerializeField] private VNUIController ui;

        [Header("Optional")]
        [SerializeField] private string startKnot = "start"; // 비우면 점프 안 함

        private readonly InkStoryEngine engine = new();

        private void Start()
        {
            if (compiledInkJson == null)
            {
                Debug.LogError("[VNInkBootstrap] compiledInkJson이 비어 있습니다. 컴파일된 JSON(TextAsset)을 연결하세요.");
                return;
            }

            if (ui == null)
            {
                Debug.LogError("[VNInkBootstrap] ui가 비어 있습니다. VNUIController를 연결하세요.");
                return;
            }

            try
            {
                engine.Initialize(compiledInkJson);

                if (!string.IsNullOrWhiteSpace(startKnot))
                {
                    engine.JumpTo(startKnot);
                }

                Debug.Log($"[VNInkBootstrap] Init OK. canContinue={engine.CanContinue()}, choices={engine.GetCurrentChoices().Count}");
                StartCoroutine(Run());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VNInkBootstrap] Ink init failed: {e}");
            }
        }

        private IEnumerator Run()
        {
            while (true)
            {
                while (engine.CanContinue())
                {
                    var line = engine.ContinueLine();

                    // Speaker가 비어있으면 기존 화자 유지
                    if (!string.IsNullOrWhiteSpace(line.Speaker))
                        ui.SetSpeaker(line.Speaker);

                    yield return ui.PresentLine(line.Text);

                    ui.AddBacklog(line.Speaker, line.Text);
                    yield return ui.WaitForAdvanceOrAuto(line.Text.Length);
                }

                var choices = engine.GetCurrentChoices();
                if (choices != null && choices.Count > 0)
                {
                    int selected = -1;
                    ui.ShowChoices(choices, idx => selected = idx);

                    while (selected < 0)
                        yield return null;

                    // 선택한 문장도 백로그에 기록
                    ui.AddBacklog("주인공", choices[selected].text);

                    engine.ChooseChoiceIndex(selected);
                    continue;
                }

                Debug.Log("[VNInkBootstrap] Story ended (no continue, no choices).");
                break;
            }
        }
    }
}