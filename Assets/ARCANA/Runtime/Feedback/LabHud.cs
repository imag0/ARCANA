using System;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

namespace Arcana
{
    public sealed class LabHud : MonoBehaviour
    {
        public LabInput input;public FirstPersonMotor motor;public ExperimentState experiment;public DrawingTool tool;public ProbeInstrument probe;public ExperimentPersistence persistence;
        public TMP_Text title,status,instrument,hint,notice,diagnostics,controls;
        public GameObject pausePanel,controlsPanel,diagnosticsPanel;
        public UnityEngine.UI.Slider sensitivity,fov;public UnityEngine.UI.Toggle invert;
        public UnityEngine.UI.Button resume,save,load,reset,quit;
        float nextRefresh,frameSum;int frames;public float AverageFps {get;private set;}
        void Start()
        {
            sensitivity.value=motor.sensitivity;fov.value=motor.view.fieldOfView;invert.isOn=motor.invertY;
            sensitivity.onValueChanged.AddListener(_=>Settings());fov.onValueChanged.AddListener(_=>Settings());invert.onValueChanged.AddListener(_=>Settings());
            resume.onClick.AddListener(()=>input.SetPaused(false));save.onClick.AddListener(()=>persistence.Save());load.onClick.AddListener(()=>persistence.Load());reset.onClick.AddListener(()=>{tool.Finish();experiment.ResetExperiment();motor.ResetPosition();});quit.onClick.AddListener(()=>Application.Quit());
            input.PauseChanged+=Pause;Pause(input.Paused);var text=new StringBuilder("CONTROLS\n\n");
            foreach(var action in input.actions.FindActionMap("Gameplay").actions){text.Append(action.name.PadRight(16));text.AppendLine(action.GetBindingDisplayString());}
            text.AppendLine("\nEscape: pause / resume\nB: bonded / insulated contact\nE: select stroke or use labelled station\nJ: join a bonded endpoint within 0.15 ell\nTool 3 + wheel/Q/R: change fixture displacement\nTool 4 + wheel: probe distance; Q/R: orientation\n\nOne ell = one metre. Sources require mechanical work.\nRigid planar panels, one scalar polarization.\nGraph junctions are an engineering approximation.");controls.text=text.ToString();
        }
        void OnDestroy(){if(input!=null)input.PauseChanged-=Pause;}
        void Settings()=>motor.Settings(sensitivity.value,invert.isOn,fov.value);
        void Pause(bool value){pausePanel.SetActive(value);if(!value)controlsPanel.SetActive(false);}
        void Update()
        {
            frameSum+=Time.unscaledDeltaTime;frames++;if(frameSum>=1){AverageFps=frames/frameSum;frameSum=0;frames=0;}
            if(input.Pressed("Controls")){controlsPanel.SetActive(!controlsPanel.activeSelf);if(controlsPanel.activeSelf)input.SetPaused(true);}
            if(input.Pressed("Diagnostics"))diagnosticsPanel.SetActive(!diagnosticsPanel.activeSelf);
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;
            string[] names={"","DRAWING PEN","CUT / ERASE","MECHANICAL FIXTURE","RESONATOR"};
            title.text="ARCANA  /  "+names[tool.Tool];
            status.text=$"{(tool.bonded?"BONDED CONTACT":"INSULATED CONTACT")}  ·  section r={tool.radius:F3} ell\nReserve {experiment.Ledger.reserve:E3}   |   strain {experiment.Mechanics.maxStrain:P2}\n{experiment.Phase}  ·  {experiment.Mechanics.graph.components.Count} components";
            var reading=probe.Reading;
            instrument.text=probe.Pending?"RESONATOR\nCOMPUTING…\nPrevious reading is stale":reading==null?"RESONATOR\nAwaiting experiment":!reading.available?"RESONATOR\nUNAVAILABLE\n"+reading.status:$"RESONATOR   /   Δf/f\n{reading.shift*1e6:+0.000000;-0.000000;0.000000} ppm\n{reading.shift:E9}  ·  {(reading.Resolved?"above resolution":"below 1e-8 resolution")}";
            hint.text=tool.Hint;notice.text=experiment.Notice;
            if(diagnosticsPanel.activeSelf)
            {
                probe.RequestEnergy(); var t=reading?.center??default;double bound=probe.Cache?.normBound??0;var m=experiment.config.model;
                diagnostics.text=$"WEAK-STATIC DIAGNOSTICS\n{Arcana.Core.ReferenceVerification.Summary}\n{(bound>.001?"WARNING: norm bound exceeds W branch ball 0.001":"Norm bound within W branch ball (not S1 acceptance)")}\nGraph rev {experiment.Revision}; measured rev {probe.PublishedRevision}\nθ = [{t.xx:E3}, {t.yy:E3}, {t.zz:E3}; {t.xy:E3}, {t.xz:E3}, {t.yz:E3}]\nNumerical estimate: {reading?.error:E3} (tol 1e-9)\nW reserve 1e-7; total B=1.21e-7; |signal| >= 10B conditional\nSensor resolution 1e-8; no full-model acceptance\nConservative source norm bound: {bound:E3}\n|g|·bound={Math.Abs(m.g)*bound:E3}  b/a·bound={m.b/m.a*bound:E3}\nd/a·bound²={m.d/m.a*bound*bound:E3}\nStrain limit 0.05; support load fraction {experiment.Mechanics.maxReaction:F4}\nFree-node force residual {experiment.Mechanics.forceResidual:E3}\n\nLEADING MECHANICAL LEDGER\nInitial {experiment.Ledger.initial:E6}  External {experiment.Ledger.external:E6}\nReserve {experiment.Ledger.reserve:E6}  Stored {experiment.Ledger.stored:E6}\nReleased/unrecovered {experiment.Ledger.released:E6}\nBalance residual {experiment.Ledger.Residual:E3}\nRelease is an unmodelled sink, not simulated heat/radiation.\n{EnergyText()}\nCoupled correction is higher order; not exact full conservation.\n\n{probe.Status}\n{reading?.route}; transverse probe field sampled on axis\nRecompute {probe.LastMilliseconds:F2} ms  |  {AverageFps:F0} FPS\nSources {probe.Cache?.sources.Length??0}; {Screen.width}×{Screen.height}\nResearch (x,y,z) -> Unity (x,z,-y); 1 ell = 1 m\nTeal strokes / junction markers are instrument overlays.";
            }
        }
        public void CloseControls(){controlsPanel.SetActive(false);}
        string EnergyText()
        {
            if(probe.EnergyPending)return "Finite-volume field energy: computing";
            var e=probe.FieldEnergy;if(e==null)return "Finite-volume field energy: pending current source cache";
            if(!e.available)return "Field energy unresolved: "+e.reason+"; est. error "+e.error.ToString("E2");
            return $"Etheta={e.field:E3} (self {e.self:E3}, cross {e.cross:E3})\nCoupled leading correction = {-e.field:E3}; error est {e.error:E2}";
        }
    }
}

