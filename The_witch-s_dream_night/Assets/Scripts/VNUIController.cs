using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Ink.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    public sealed class VNUIController : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text lineText;

        [Header("Choices")]
        [SerializeField] private GameObject choicesRoot;
        [SerializeField] private Transform choicesContainer;
        [SerializeField] private Button choiceButtonPrefab;

        [Header("Backlog")]
        [SerializeField] private GameObject backlogRoot;
        [SerializeField] private TMP_Text backlogText;

        [Tooltip("Backlog ScrollView의 ScrollRect를 연결하세요. (ScrollRect.content는 Content로 지정되어 있어야 합니다.)")]
        [SerializeField] private ScrollRect backlogScrollRect;

        [Tooltip("백로그 최대 기록 수. 초과하면 오래된 항목부터 삭제됩니다.")]
        [SerializeField] private int backlogMaxEntries = 300;

        [Tooltip("유저가 거의 하단을 보고 있을 때만 자동으로 하단으로 스크롤합니다. 0에 가까울수록 더 엄격합니다.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float backlogAutoScrollThreshold = 0.02f;

        [Header("Controls")]
        [SerializeField] private Button advanceButton;
        [SerializeField] private Toggle autoToggle;
        [SerializeField] private Toggle skipToggle;
        [SerializeField] private Button backlogButton;

        [Header("Typing")]
        [SerializeField] private float secondsPerChar = 0.03f;

        [Header("Auto")]
        [SerializeField] private float autoBaseWait = 0.8f;
        [SerializeField] private float autoPerCharWait = 0.02f;

        private bool advanceRequested;
        private bool skipTypingRequested;
        private bool isTyping;

        private bool advanceButtonPrevActive;

        private const float ChoiceMinHeight = 70f;
        private const float ChoiceVerticalPadding = 24f;

        private readonly List<(string speaker, string line)> backlog = new();

        public bool AutoMode => autoToggle != null && autoToggle.isOn;
        public bool SkipMode => skipToggle != null && skipToggle.isOn;

        private void Awake()
        {
            if (advanceButton != null)
                advanceButton.onClick.AddListener(OnAdvancePressed);

            if (backlogButton != null)
                backlogButton.onClick.AddListener(ToggleBacklog);

            SetChoicesVisible(false);
            SetBacklogVisible(false);
        }

        private void OnAdvancePressed()
        {
            if (isTyping)
            {
                skipTypingRequested = true;
                return;
            }

            advanceRequested = true;
        }

        public void SetSpeaker(string speaker)
        {
            if (speakerText != null)
                speakerText.text = speaker ?? string.Empty;
        }

        public IEnumerator PresentLine(string text)
        {
            text ??= string.Empty;

            if (lineText == null)
                yield break;

            if (SkipMode)
            {
                lineText.text = text;
                lineText.maxVisibleCharacters = int.MaxValue;
                isTyping = false;
                skipTypingRequested = false;
                yield break;
            }

            isTyping = true;
            skipTypingRequested = false;

            lineText.text = text;
            lineText.maxVisibleCharacters = 0;

            lineText.ForceMeshUpdate();

            float delay = Mathf.Clamp(secondsPerChar, 0.001f, 1f);

            int total = lineText.textInfo.characterCount;
            int visible = 0;
            float timer = 0f;

            while (visible < total)
            {
                if (SkipMode || skipTypingRequested)
                    break;

                timer += Time.unscaledDeltaTime;

                while (timer >= delay && visible < total)
                {
                    timer -= delay;
                    visible++;
                    lineText.maxVisibleCharacters = visible;
                }

                yield return null;
            }

            lineText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
            skipTypingRequested = false;
        }

        public IEnumerator WaitForAdvanceOrAuto(int lineCharCount)
        {
            advanceRequested = false;

            float autoWait = Mathf.Max(0.05f, autoBaseWait + lineCharCount * autoPerCharWait);
            float autoTimer = 0f;

            while (true)
            {
                if (SkipMode)
                {
                    advanceRequested = false;
                    yield break;
                }

                if (advanceRequested)
                {
                    advanceRequested = false;
                    yield break;
                }

                if (AutoMode)
                {
                    autoTimer += Time.unscaledDeltaTime;
                    if (autoTimer >= autoWait)
                        yield break;
                }
                else
                {
                    autoTimer = 0f;
                }

                yield return null;
            }
        }

        public void AddBacklog(string speaker, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            backlog.Add((speaker ?? string.Empty, line));

            if (backlogMaxEntries > 0 && backlog.Count > backlogMaxEntries)
            {
                int removeCount = backlog.Count - backlogMaxEntries;
                backlog.RemoveRange(0, removeCount);
            }

            RefreshBacklogText();

            // 백로그가 열려 있고, 유저가 거의 하단에 있을 때만 자동 스크롤
            AutoScrollBacklogIfNeeded(forceToBottom: false);
        }

        private void RefreshBacklogText()
        {
            if (backlogText == null)
                return;

            var sb = new StringBuilder(4096);

            for (int i = 0; i < backlog.Count; i++)
            {
                var e = backlog[i];

                if (!string.IsNullOrEmpty(e.speaker))
                {
                    sb.Append(e.speaker).Append(": ");
                }

                sb.Append(e.line).Append('\n');
            }

            backlogText.text = sb.ToString();
        }

        private void AutoScrollBacklogIfNeeded(bool forceToBottom)
        {
            if (backlogScrollRect == null)
                return;

            if (backlogRoot == null || !backlogRoot.activeInHierarchy)
                return;

            bool shouldScroll = forceToBottom;

            if (!shouldScroll)
            {
                // verticalNormalizedPosition: 1 = top, 0 = bottom
                float pos = backlogScrollRect.verticalNormalizedPosition;
                shouldScroll = pos <= backlogAutoScrollThreshold;
            }

            if (!shouldScroll)
                return;

            // 레이아웃 갱신 후 이동
            Canvas.ForceUpdateCanvases();

            // Content가 제대로 지정되어 있어야 함(ScrollRect.content)
            backlogScrollRect.verticalNormalizedPosition = 0f;
        }

        public void ShowChoices(IReadOnlyList<Choice> choices, Action<int> onSelect)
        {
            if (advanceButton != null)
            {
                advanceButtonPrevActive = advanceButton.gameObject.activeSelf;
                advanceButton.gameObject.SetActive(false);
            }

            ClearChoices();

            if (choices == null || choices.Count == 0)
            {
                SetChoicesVisible(false);

                if (advanceButton != null)
                    advanceButton.gameObject.SetActive(advanceButtonPrevActive);

                return;
            }

            SetChoicesVisible(true);

            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;

                var btn = Instantiate(choiceButtonPrefab, choicesContainer);

                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                {
                    tmp.text = choices[i].text;

                    tmp.ForceMeshUpdate();

                    var le = btn.GetComponent<LayoutElement>();
                    if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();

                    float h = Mathf.Max(ChoiceMinHeight, tmp.preferredHeight + ChoiceVerticalPadding);
                    le.preferredHeight = h;
                }

                btn.onClick.AddListener(() =>
                {
                    // 선택지 문장도 백로그에 기록
                    AddBacklog("선택", choices[index].text);

                    SetChoicesVisible(false);

                    if (advanceButton != null)
                        advanceButton.gameObject.SetActive(advanceButtonPrevActive);

                    onSelect?.Invoke(index);
                });
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesContainer as RectTransform);
        }

        public void ClearChoices()
        {
            if (choicesContainer == null)
                return;

            for (int i = choicesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(choicesContainer.GetChild(i).gameObject);
            }
        }

        public void ToggleBacklog()
        {
            if (backlogRoot == null)
                return;

            bool next = !backlogRoot.activeSelf;
            backlogRoot.SetActive(next);

            if (next)
            {
                // 열 때 최신 상태 반영 + 하단으로 강제 이동
                RefreshBacklogText();
                AutoScrollBacklogIfNeeded(forceToBottom: true);
            }
        }

        private void SetChoicesVisible(bool visible)
        {
            if (choicesRoot != null)
                choicesRoot.SetActive(visible);
        }

        private void SetBacklogVisible(bool visible)
        {
            if (backlogRoot != null)
                backlogRoot.SetActive(visible);
        }
    }
}