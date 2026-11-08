using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arcana
{
    public sealed class LabInput : MonoBehaviour
    {
        public InputActionAsset actions;
        public bool Paused {get;private set;}=true;
        public bool Focused {get;private set;}=true;
        public event Action<bool> PauseChanged;
        public InputAction Action(string name)=>actions.FindAction("Gameplay/"+name,true);
        public bool Pressed(string name)=>!Paused&&Action(name).WasPressedThisFrame();
        public bool Held(string name)=>!Paused&&Action(name).IsPressed();
        public Vector2 Read2(string name)=>Paused?Vector2.zero:Action(name).ReadValue<Vector2>();
        void Awake(){Application.targetFrameRate=120;actions=Instantiate(actions);actions.Disable();actions.FindActionMap("UI").Enable();}
        void Update(){if(Arcana.Elemental.ComposerInputField.LastFocusedFrame>=Time.frameCount-1)return;if(actions.FindAction("UI/Pause").WasPressedThisFrame()&&Focused)SetPaused(!Paused);}
        public void SetPaused(bool paused)
        {
            Paused=paused||!Focused;
            if(Paused){actions.FindActionMap("Gameplay").Disable();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else{actions.FindActionMap("Gameplay").Enable();Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            PauseChanged?.Invoke(Paused);
        }
        void OnApplicationFocus(bool focus){Focused=focus;if(!focus)SetPaused(true);}
        void OnApplicationPause(bool paused){if(paused)SetPaused(true);}
        void OnDestroy(){if(actions!=null){actions.Disable();Destroy(actions);}Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
