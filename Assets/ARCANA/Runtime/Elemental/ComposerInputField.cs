using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Arcana.Elemental
{
    // TMP's stock field reads the native IMGUI event queue. Use the project's
    // Input System for numeric editing too, including synthetic-device tests.
    public sealed class ComposerInputField : TMP_InputField
    {
        public static int LastFocusedFrame=-100;Keyboard keyboard;
        protected override void LateUpdate()
        {
            base.LateUpdate();if(isFocused)LastFocusedFrame=Time.frameCount;
            if(keyboard==Keyboard.current)return;
            if(keyboard!=null)keyboard.onTextInput-=TextEntered;
            keyboard=Keyboard.current;if(keyboard!=null)keyboard.onTextInput+=TextEntered;
        }
        protected override void OnDisable(){if(keyboard!=null)keyboard.onTextInput-=TextEntered;keyboard=null;base.OnDisable();}
        void TextEntered(char ch)
        {
            if(!isFocused||char.IsControl(ch))return;
            if(keyboard!=null&&(keyboard.ctrlKey.isPressed||keyboard.altKey.isPressed))return;
            ProcessEvent(new Event{type=EventType.KeyDown,character=ch});ForceLabelUpdate();
        }
        public override void OnUpdateSelected(BaseEventData data)
        {
            if(!isFocused||keyboard==null)return;
            var modifiers=(keyboard.ctrlKey.isPressed?EventModifiers.Control:0)|(keyboard.shiftKey.isPressed?EventModifiers.Shift:0);
            void Key(UnityEngine.InputSystem.Controls.KeyControl key,KeyCode code){if(key.wasPressedThisFrame)ProcessEvent(new Event{type=EventType.KeyDown,keyCode=code,modifiers=modifiers});}
            Key(keyboard.backspaceKey,KeyCode.Backspace);Key(keyboard.deleteKey,KeyCode.Delete);Key(keyboard.leftArrowKey,KeyCode.LeftArrow);Key(keyboard.rightArrowKey,KeyCode.RightArrow);Key(keyboard.homeKey,KeyCode.Home);Key(keyboard.endKey,KeyCode.End);
            if(keyboard.ctrlKey.isPressed){Key(keyboard.aKey,KeyCode.A);Key(keyboard.cKey,KeyCode.C);Key(keyboard.xKey,KeyCode.X);Key(keyboard.vKey,KeyCode.V);}
            if(keyboard.enterKey.wasPressedThisFrame||keyboard.escapeKey.wasPressedThisFrame||keyboard.tabKey.wasPressedThisFrame){DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);}
            ForceLabelUpdate();data.Use();
        }
    }
}
