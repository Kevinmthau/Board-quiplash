using System;
using System.Text;
using Board.Core;
using Board.Input;
using Board.Save;
using Board.Session;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace TableLaughs
{
    public sealed class BoardInputBridge : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private bool logBoardContacts;
        [SerializeField] [Min(0.1f)] private float boardContactLogIntervalSeconds = 1f;

        private readonly StringBuilder boardContactLogBuilder = new StringBuilder();
        private float nextBoardContactLogTime;
#endif

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BoardApplication.SetPauseScreenContext("Table Laughs", false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (logBoardContacts)
            {
                LogBoardContacts();
            }
        }
#endif

        public BoardUIInputModule EnsureBoardUiInputModule(EventSystem eventSystem)
        {
            var boardInputModule = eventSystem.GetComponent<BoardUIInputModule>();
            if (boardInputModule == null)
            {
                boardInputModule = eventSystem.gameObject.AddComponent<BoardUIInputModule>();
            }

#if UNITY_EDITOR
            // Editor uses the new Input System UI module so Board-required input settings can
            // stay unchanged while mouse, touch, and pen input still work in Play Mode.
            boardInputModule.forceModuleActive = false;
            EnsureEditorInputModule(eventSystem);
#else
            // TODO(Board SDK): On hardware this should remain the primary UI input module.
            boardInputModule.forceModuleActive = true;
#endif
            return boardInputModule;
        }

#if UNITY_EDITOR
        private static void EnsureEditorInputModule(EventSystem eventSystem)
        {
            var editorInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (editorInputModule == null)
            {
                editorInputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            editorInputModule.enabled = true;
            if (editorInputModule.actionsAsset == null)
            {
                editorInputModule.AssignDefaultActions();
            }

            WarnIfEditorPointerBindingMissing(editorInputModule.actionsAsset, "Point", "<Mouse>/position", "mouse position");
            WarnIfEditorPointerBindingMissing(editorInputModule.actionsAsset, "Click", "<Mouse>/leftButton", "mouse click");
            WarnIfEditorPointerBindingMissing(editorInputModule.actionsAsset, "Point", "<Touchscreen>/touch*/position", "touch position");
            WarnIfEditorPointerBindingMissing(editorInputModule.actionsAsset, "Click", "<Touchscreen>/touch*/press", "touch press");
            WarnIfEditorPointerBindingMissing(editorInputModule.actionsAsset, "Point", "<Pen>/position", "pen position");
            WarnIfEditorPointerBindingMissing(editorInputModule.actionsAsset, "Click", "<Pen>/tip", "pen tip");
        }

        private static void WarnIfEditorPointerBindingMissing(InputActionAsset actionsAsset, string actionName, string path, string label)
        {
            if (actionsAsset == null)
            {
                Debug.LogWarning("[Table Laughs] Editor UI input has no action asset; mouse, touch, and pen input may not route to UI.");
                return;
            }

            foreach (var binding in actionsAsset.bindings)
            {
                if (BindingMatchesAction(binding.action, actionName) &&
                    (string.Equals(binding.effectivePath, path, StringComparison.Ordinal) ||
                     string.Equals(binding.path, path, StringComparison.Ordinal)))
                {
                    return;
                }
            }

            Debug.LogWarning($"[Table Laughs] Editor UI input action asset is missing {label} binding '{path}'. Stylus/touch drawing may not work in Play Mode.");
        }

        private static bool BindingMatchesAction(string bindingActionName, string expectedActionName)
        {
            if (string.IsNullOrEmpty(bindingActionName))
            {
                return false;
            }

            return string.Equals(bindingActionName, expectedActionName, StringComparison.Ordinal) ||
                   bindingActionName.EndsWith("/" + expectedActionName, StringComparison.Ordinal);
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogBoardContacts()
        {
            if (Time.unscaledTime < nextBoardContactLogTime)
            {
                return;
            }

            nextBoardContactLogTime = Time.unscaledTime + boardContactLogIntervalSeconds;
            var fingers = BoardInput.GetActiveContacts(BoardContactType.Finger);
            var blobs = BoardInput.GetActiveContacts(BoardContactType.Blob);
            var glyphs = BoardInput.GetActiveContacts(BoardContactType.Glyph);

            if (fingers.Length == 0 && blobs.Length == 0 && glyphs.Length == 0)
            {
                return;
            }

            boardContactLogBuilder.Clear();
            boardContactLogBuilder.Append("[Table Laughs] Board contacts");
            AppendContactSummary("Finger", fingers);
            AppendContactSummary("Blob", blobs);
            AppendContactSummary("Glyph", glyphs);
            Debug.Log(boardContactLogBuilder.ToString());
        }

        private void AppendContactSummary(string label, BoardContact[] contacts)
        {
            if (contacts.Length == 0)
            {
                return;
            }

            boardContactLogBuilder.Append(" | ");
            boardContactLogBuilder.Append(label);
            boardContactLogBuilder.Append(": ");

            for (var i = 0; i < contacts.Length; i++)
            {
                if (i > 0)
                {
                    boardContactLogBuilder.Append(", ");
                }

                var contact = contacts[i];
                boardContactLogBuilder.Append('#');
                boardContactLogBuilder.Append(contact.contactId);
                boardContactLogBuilder.Append(' ');
                boardContactLogBuilder.Append(contact.phase);
                boardContactLogBuilder.Append(" pos=");
                boardContactLogBuilder.Append(contact.screenPosition);
            }
        }
#endif

        public void NoteFutureBoardIntegrations()
        {
            // TODO(Board SDK): Use BoardInput.GetActiveContacts(...) to map contact positions
            // to seat zones when the game needs richer per-touch/per-stylus routing than seat panels.
            // TODO(Board SDK): If Table Laughs later supports Board profiles, read
            // BoardSession.players for profile names/colors instead of local-only players.
            // TODO(Board SDK): Use BoardSaveGameManager for optional saved house prompt packs.
            _ = typeof(BoardSession);
            _ = typeof(BoardSaveGameManager);
        }
    }
}
