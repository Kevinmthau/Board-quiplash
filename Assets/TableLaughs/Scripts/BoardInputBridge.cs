using Board.Core;
using Board.Input;
using Board.Save;
using Board.Session;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEngine.InputSystem.UI;
#endif

namespace TableLaughs
{
    public sealed class BoardInputBridge : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
            BoardApplication.SetPauseScreenContext("Table Laughs", false);
        }

        public BoardUIInputModule EnsureBoardUiInputModule(EventSystem eventSystem)
        {
            var boardInputModule = eventSystem.GetComponent<BoardUIInputModule>();
            if (boardInputModule == null)
            {
                boardInputModule = eventSystem.gameObject.AddComponent<BoardUIInputModule>();
            }

#if UNITY_EDITOR
            // Editor uses the new Input System UI module so Board-required input settings can
            // stay unchanged while mouse clicks still work in Play Mode.
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

            if (editorInputModule.actionsAsset == null)
            {
                editorInputModule.AssignDefaultActions();
            }
        }
#endif

        public void NoteFutureBoardIntegrations()
        {
            // TODO(Board SDK): Use BoardInput.GetActiveContacts(...) to map contact positions
            // to seat zones when the game needs richer per-touch routing than seat panels.
            // TODO(Board SDK): If Table Laughs later supports Board profiles, read
            // BoardSession.players for profile names/colors instead of local-only players.
            // TODO(Board SDK): Use BoardSaveGameManager for optional saved house prompt packs.
            _ = typeof(BoardSession);
            _ = typeof(BoardSaveGameManager);
        }
    }
}
