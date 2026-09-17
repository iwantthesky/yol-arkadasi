using UnityEngine;

namespace DortCuce.UnityGame
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MotorSound : MonoBehaviour
    {
        private AudioSource source;
        public bool Muted;
        private void Awake()
        {
            source=GetComponent<AudioSource>(); source.loop=true; source.spatialBlend=0;
            var samples=new float[22050];
            var random=new System.Random(73);
            for(int i=0;i<samples.Length;i++)
            {float phase=i*2*Mathf.PI*55/22050f;samples[i]=(Mathf.Sin(phase)*.45f+Mathf.Sin(phase*2)*.20f+Mathf.Sin(phase*3)*.08f+(float)(random.NextDouble()-.5)*.10f)*.3f;}
            var clip=AudioClip.Create("Generated engine • original",samples.Length,1,22050,false);
            clip.SetData(samples,0); source.clip=clip;source.Play();
        }
        public void Apply(BikeSnapshot s,bool playing)
        {source.pitch=Mathf.Lerp(source.pitch,Mathf.Clamp(s.rpm/1600f,.65f,3.5f),Time.deltaTime*8);source.volume=Mathf.Lerp(source.volume,Muted||!playing||!s.engineRunning?0:.10f+s.speed*.002f,Time.deltaTime*6);}
        private void OnDestroy(){if(source&&source.clip)Destroy(source.clip);}
    }
}
