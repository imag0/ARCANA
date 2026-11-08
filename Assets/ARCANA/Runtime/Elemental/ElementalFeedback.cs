using System;
using System.Linq;
using UnityEngine;
namespace Arcana.Elemental
{
    public sealed class ElementalFeedback : MonoBehaviour
    {
        public static float MusicGain{get;private set;}=1;public static float SfxGain{get;private set;}=1;
        public ElementalWorld world;public int PlayedEvents{get;private set;}AudioSource[] voices;AudioSource fire,wind,ambient,ui;AudioClip seal,release,impact,click;int next;float lastImpact;
        public void SetMusicGain(float gain){MusicGain=Mathf.Clamp01(gain);PlayerPrefs.SetFloat("ArcanaAmbientGain",MusicGain);ApplyGains();}
        public void SetSfxGain(float gain){SfxGain=Mathf.Clamp01(gain);PlayerPrefs.SetFloat("ArcanaSfxGain",SfxGain);ApplyGains();}
        void ApplyGains(){if(voices==null)return;foreach(var voice in voices)voice.volume=SfxGain;ui.volume=SfxGain;ambient.volume=.14f*MusicGain;}
        AudioSource Voice(string label){var source=new GameObject(label).AddComponent<AudioSource>();source.transform.SetParent(transform);source.playOnAwake=false;source.spatialBlend=1;source.minDistance=2;source.maxDistance=35;source.rolloffMode=AudioRolloffMode.Linear;return source;}
        void Awake()
        {
            MusicGain=PlayerPrefs.GetFloat("ArcanaAmbientGain",1);SfxGain=PlayerPrefs.GetFloat("ArcanaSfxGain",1);
            voices=Enumerable.Range(0,8).Select(i=>Voice("Elemental feedback "+i)).ToArray();seal=Tone("Seal closes",.36f,523,784);release=Tone("Control releases",.18f,330,165);impact=Tone("Material contact",.12f,160,72);click=Tone("Notebook click",.035f,1200,700);
            fire=Voice("Heat source sound");wind=Voice("Airflow sound");fire.clip=Noise("Fire crackle",17);wind.clip=Noise("Air motion",71);fire.loop=wind.loop=true;fire.Play();wind.Play();
            ambient=Voice("Courtyard air");ambient.spatialBlend=0;ambient.clip=Noise("Courtyard room tone",127);ambient.loop=true;ambient.Play();ui=Voice("Notebook interface sound");ui.spatialBlend=0;ui.ignoreListenerPause=true;ApplyGains();
        }
        static AudioClip Tone(string name,float seconds,float from,float to)
        {const int rate=22050;var data=new float[(int)(rate*seconds)];double phase=0;for(int i=0;i<data.Length;i++){float t=i/(float)data.Length;phase+=2*Math.PI*Mathf.Lerp(from,to,t)/rate;data[i]=(float)Math.Sin(phase)*Mathf.Sin(Mathf.PI*t)*Mathf.Exp(-t*3)*.4f;}var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;}
        static AudioClip Noise(string name,int seed)
        {const int rate=22050,n=rate*4;var data=new float[n];var random=new System.Random(seed);float low=0;for(int i=0;i<n;i++){low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.045f);float fade=Mathf.Min(1,Mathf.Min(i,n-1-i)/(rate*.005f));data[i]=low*.5f*fade;}var clip=AudioClip.Create(name,n,1,rate,false);clip.SetData(data,0);return clip;}
        void Play(AudioClip clip,Vector3 point,float gain){var source=voices[next++%voices.Length];source.transform.position=point;source.PlayOneShot(clip,gain);PlayedEvents++;}
        public void Cast(Vector3 point)=>Play(seal,point,.8f);
        public void Release(Vector3 point)=>Play(release,point,.6f);
        public void Click(){ui.PlayOneShot(click,.3f);PlayedEvents++;}
        public void Impact(Vector3 point,float speed){if(Time.time-lastImpact<.06f)return;lastImpact=Time.time;Play(impact,point,Mathf.Clamp01(speed/10)*.5f);}
        void Update()
        {
            if(world==null)return;var burning=world.bodies.FirstOrDefault(b=>b.burning);var activeFire=world.casts.Where(c=>c.active).SelectMany(c=>c.channels).FirstOrDefault(c=>c.element==Rune.Fire);
            fire.volume=(burning!=null||activeFire!=null?.35f:0)*SfxGain;if(burning!=null)fire.transform.position=burning.Body.position;else if(activeFire!=null)fire.transform.position=activeFire.anchor;
            var flow=world.WindAt(world.motor.transform.position+Vector3.up);wind.transform.position=world.motor.transform.position;wind.volume=Mathf.Clamp01(flow.magnitude/12)*.3f*SfxGain;
        }
    }
}
