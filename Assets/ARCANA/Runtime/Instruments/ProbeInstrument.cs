using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Arcana.Core;
using Arcana.Field;

namespace Arcana
{
    public sealed class ProbeInstrument : MonoBehaviour
    {
        public ExperimentState experiment;public Transform mount,rod;
        public double length=.05,radius=.002;public float distance=.5f,angle;
        public Measurement Reading {get;private set;}
        public string Status {get;private set;}="Pending";
        public double LastMilliseconds {get;private set;}
        public SourceCache Cache {get;private set;}
        public EnergyEstimate FieldEnergy {get;private set;}
        public bool EnergyPending {get;private set;}
        Task<EnergyEstimate> energyTask;CancellationTokenSource energyCancel;int energyRevision=-1;
        public int PublishedRevision {get;private set;}=-1;
        public int PoseRevision {get;private set;}
        public bool Pending {get;private set;}
        CancellationTokenSource cancel;Task<WorkResult> task;int requestedRevision,requestedPose;float due;bool rebuild=true;
        DrawingSurface panel;D3 localPoint;bool positioned;
        sealed class WorkResult{public SourceCache cache;public Measurement measurement;public double ms;}
        struct SourceSpec {public D3 a,b;public double radius,C,strain;}
        public D3 Position=>Coordinates.Data(mount.position);
        public D3 Axis=>Coordinates.Data(mount.right);
        void OnEnable(){experiment.Changed+=SourcesChanged;SourcesChanged();}
        void OnDisable(){experiment.Changed-=SourcesChanged;cancel?.Cancel();energyCancel?.Cancel();}
        void SourcesChanged(){rebuild=true;FieldEnergy=null;energyCancel?.Cancel();Schedule();}
        public void RequestEnergy()
        {
            if(Pending||Cache==null||energyTask!=null||energyRevision==experiment.Revision&&FieldEnergy!=null)return;
            energyCancel?.Dispose();energyCancel=new CancellationTokenSource();var token=energyCancel.Token;var cache=Cache;energyRevision=experiment.Revision;EnergyPending=true;energyTask=Task.Run(()=>cache.FieldEnergy(token),token);
        }
        void Schedule(){cancel?.Cancel();Pending=true;Status="Computing / previous reading stale";experiment.Phase=ExperimentPhase.Computing;due=Time.unscaledTime+.12f;}
        public void Place(DrawingSurface surface,D3 point){panel=surface;localPoint=point;positioned=true;UpdatePose();}
        public void Rotate(float degrees){angle+=degrees;UpdatePose();}
        public void ChangeDistance(float delta){distance=Mathf.Clamp(distance+delta,.09f,1.5f);UpdatePose();}
        void UpdatePose()
        {
            if(panel!=null){Vector3 normal=-panel.transform.forward;mount.position=panel.World(localPoint)+normal*distance;var axis=panel.transform.right*Mathf.Cos(angle*Mathf.Deg2Rad)+panel.transform.up*Mathf.Sin(angle*Mathf.Deg2Rad);mount.rotation=Quaternion.LookRotation(Vector3.Cross(axis,normal),normal);mount.right=axis;}
            PoseRevision++;Schedule();
        }
        public void Restore(D3 position,D3 axis,double probeLength,double probeRadius)
        {
            panel=null;positioned=true;mount.position=Coordinates.Vector(position);mount.right=Coordinates.Vector(axis);length=probeLength;radius=probeRadius;
            // Recover the placement frame from the saved world pose, so wheel/rotation work after loading.
            float best=float.PositiveInfinity;
            foreach(var surface in experiment.surfaces)
            {
                var local=surface.transform.InverseTransformPoint(mount.position);float separation=-local.z;local.z=0;
                if(separation<.085f||separation>1.51f||separation>=best||!surface.Contains(Coordinates.Data(local)))continue;
                panel=surface;localPoint=Coordinates.Data(local);distance=separation;best=separation;
                angle=Mathf.Atan2(Vector3.Dot(mount.right,surface.transform.up),Vector3.Dot(mount.right,surface.transform.right))*Mathf.Rad2Deg;
            }
            PoseRevision++;Schedule();
        }
        void Start(){if(!positioned)Place(experiment.Surface("paper"),default);}
        void Update()
        {
            if(energyTask!=null&&energyTask.IsCompleted){if(energyTask.Status==TaskStatus.RanToCompletion&&energyRevision==experiment.Revision&&!energyCancel.IsCancellationRequested)FieldEnergy=energyTask.Result;energyTask=null;EnergyPending=false;}
            if(task!=null&&task.IsCompleted)
            {
                if(task.Status==TaskStatus.RanToCompletion&&requestedRevision==experiment.Revision&&requestedPose==PoseRevision&&!cancel.IsCancellationRequested){var r=task.Result;Cache=r.cache;Reading=r.measurement;LastMilliseconds=r.ms;PublishedRevision=requestedRevision;Pending=false;Status=Reading.status;experiment.Phase=Reading.available?ExperimentPhase.Measured:ExperimentPhase.Unsupported;}
                else if(task.IsFaulted){var error=task.Exception?.GetBaseException();if(!(error is OperationCanceledException)){Status="Calculation failed: "+error?.Message;Pending=false;UnityEngine.Debug.LogError(Status);}}
                task=null;
            }
            if(!Pending||task!=null||Time.unscaledTime<due)return;
            requestedRevision=experiment.Revision;requestedPose=PoseRevision;cancel?.Dispose();cancel=new CancellationTokenSource();var token=cancel.Token;
            var specifications=new List<SourceSpec>();var settings=experiment.config.model;
            var model=new Model{I=settings.I,K=settings.K,a=settings.a,b=settings.b,d=settings.d,g=settings.g};
            bool needsSources=rebuild||Cache==null||PublishedRevision!=experiment.Revision;
            if(needsSources)
            {
                foreach(var edge in experiment.Mechanics.graph.edges)
                {
                    if(Math.Abs(edge.strain)<1e-12)continue;var surface=experiment.Surface(edge.stroke.surface);var nodes=experiment.Mechanics.graph.nodes;
                    specifications.Add(new SourceSpec{a=Coordinates.ToResearch(surface.World(nodes[edge.a].position)),b=Coordinates.ToResearch(surface.World(nodes[edge.b].position)),radius=edge.stroke.radius,C=edge.stroke.C,strain=edge.strain});
                }
            }
            var oldCache=needsSources?null:Cache;rebuild=false;var position=Coordinates.ToResearch(mount.position);var axis=Coordinates.ToResearch(mount.right);double probeLength=length,probeRadius=radius;
            task=Task.Run(()=>{var clock=Stopwatch.StartNew();var cache=oldCache??new SourceCache(specifications.ConvertAll(s=>new Cylinder(s.a,s.b,s.radius,s.C,s.strain,model)),model);var measurement=cache.Measure(position,axis,probeLength,probeRadius,token);return new WorkResult{cache=cache,measurement=measurement,ms=clock.Elapsed.TotalMilliseconds};},token);
        }
    }
}
