using System;
using System.Collections.Generic;
using UnityEngine;

namespace DortCuce.UnityGame
{
    public sealed class MotorBikeView : MonoBehaviour
    {
        [Serializable] private class MaterialSpec { public string name; public float[] color; public float roughness, metallic; }
        [Serializable] private class MaterialFile { public MaterialSpec[] materials; }
        private Transform front, rear;
        private readonly List<Transform> riders = new();
        private readonly List<Material> owned = new();
        private float wheelAngle, displayedBalance;
        private Quaternion frontBase, rearBase;
        public int MeshCount { get; private set; }

        public void Build()
        {
            var asset = Resources.Load<GameObject>("Motorcycle/EnginMotor");
            if (!asset) throw new InvalidOperationException("EnginMotor.fbx bulunamadı.");
            var model = Instantiate(asset, transform);
            model.name = "Engin d.blend • oyun modeli";
            foreach (var t in model.GetComponentsInChildren<Transform>())
            {
                if (t.name == "FrontWheel") front = t;
                if (t.name == "RearWheel") rear = t;
            }
            if (!front || !rear) throw new InvalidOperationException("Motor teker pivotları eksik.");
            if (transform.InverseTransformPoint(front.position).z < transform.InverseTransformPoint(rear.position).z)
                model.transform.localRotation = Quaternion.Euler(0, 180, 0) * model.transform.localRotation;
            frontBase = front.localRotation; rearBase = rear.localRotation;
            var colors = JsonUtility.FromJson<MaterialFile>(Resources.Load<TextAsset>("Motorcycle/materials").text);
            var palette = new Dictionary<string, Material>();
            foreach (var spec in colors.materials)
            {
                var mat = new Material(Shader.Find("Standard")) { name = spec.name };
                mat.color = new Color(spec.color[0], spec.color[1], spec.color[2], 1);
                // Preserve source base colors. Blender procedural microtextures are not baked.
                mat.SetFloat("_Metallic", Mathf.Min(.8f, spec.metallic));
                mat.SetFloat("_Glossiness", Mathf.Clamp(1 - spec.roughness, .15f, .8f));
                if (spec.name.Contains("LED_") || spec.name.Contains("Glow"))
                { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", mat.color * .5f); }
                palette[spec.name] = mat; owned.Add(mat);
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                MeshCount++;
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] && palette.TryGetValue(mats[i].name.Replace(" (Instance)", ""), out var replacement)) mats[i] = replacement;
                }
                renderer.sharedMaterials = mats;
            }
            Color[] jackets = { new(.92f,.44f,.19f), new(.25f,.64f,.61f), new(.86f,.70f,.32f), new(.60f,.46f,.72f) };
            for (int i = 0; i < 4; i++)
            {
                var rider = new GameObject("Yol arkadaşı " + (i + 1)).transform;
                rider.SetParent(transform, false);
                rider.localPosition = new Vector3(i < 2 ? 0 : (i == 2 ? -.39f : .39f), 1.00f, .27f - (i % 2) * .62f);
                Make(rider,"Mont",PrimitiveType.Capsule,new(0,.22f,0),new(.30f,.24f,.25f),jackets[i]);
                Make(rider,"Kask",PrimitiveType.Sphere,new(0,.61f,.05f),Vector3.one*.31f,new(.93f,.87f,.72f));
                Make(rider,"Vizör",PrimitiveType.Sphere,new(0,.62f,.16f),new(.27f,.13f,.10f),new(.055f,.12f,.14f));
                Make(rider,"Sırt çantası",PrimitiveType.Cube,new(0,.25f,-.17f),new(.25f,.3f,.14f),jackets[i]*.64f);
                foreach (float side in new[]{-1f,1f})
                {
                    var arm = Make(rider,"Kol",PrimitiveType.Capsule,new(side*.2f,.24f,.13f),new(.10f,.19f,.10f),jackets[i]);
                    arm.localRotation=Quaternion.Euler(42,0,side*20);
                    var leg = Make(rider,"Bacak",PrimitiveType.Capsule,new(side*.18f,-.07f,.08f),new(.12f,.2f,.12f),new(.085f,.14f,.15f));
                    leg.localRotation=Quaternion.Euler(-30,0,side*15);
                }
                riders.Add(rider);
            }
            var pole = Make(riders[3],"Denge çubuğu",PrimitiveType.Cylinder,new(0,.30f,.20f),new(.045f,1.25f,.045f),new(.82f,.76f,.56f));
            pole.localRotation=Quaternion.Euler(0,0,90);
        }

        private Transform Make(Transform parent,string label,PrimitiveType shape,Vector3 position,Vector3 scale,Color color)
        {
            var o=GameObject.CreatePrimitive(shape); o.name=label; Destroy(o.GetComponent<Collider>());
            o.transform.SetParent(parent,false); o.transform.localPosition=position; o.transform.localScale=scale;
            var m=new Material(Shader.Find("Standard")){color=color}; m.SetFloat("_Glossiness",.2f); owned.Add(m);
            o.GetComponent<Renderer>().sharedMaterial=m; return o.transform;
        }

        public void Animate(BikeSnapshot s,MotorInput input,float dt)
        {
            var p=MotorCourse.Sample(s.distance);
            float worldX=p.centerX+s.lateral;
            float dx=Mathf.Sin(s.heading),dz=Mathf.Cos(s.heading);
            float ahead=MotorCourse.GroundHeight(s.distance+dz,worldX+dx);
            float current=MotorCourse.GroundHeight(s.distance,worldX);
            var yaw=s.heading*Mathf.Rad2Deg;
            var pitch=-Mathf.Atan2(ahead-current,1)*Mathf.Rad2Deg;
            var target=new Vector3(worldX,s.elevation+.015f,s.distance);
            transform.position=Vector3.Lerp(transform.position,target,1-Mathf.Exp(-16*dt));
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(pitch,yaw,-s.lean*Mathf.Rad2Deg),1-Mathf.Exp(-15*dt));
            wheelAngle += s.speed/.435f*Mathf.Rad2Deg*dt;
            // Exported wheel meshes have their pivot at the axle. Rotate in motorcycle space.
            if(front) front.localRotation=Quaternion.AngleAxis(s.steeringAngle*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(wheelAngle,Vector3.right)*frontBase;
            if(rear) rear.localRotation=Quaternion.AngleAxis(wheelAngle,Vector3.right)*rearBase;
            float visualBalance=Mathf.Abs(input.balance)>.75f?input.balance:Mathf.Clamp(input.balance+input.steer*.65f,-1f,1f);
            displayedBalance=Mathf.MoveTowards(displayedBalance,visualBalance,dt*3.2f);
            for(int i=0;i<riders.Count;i++)
                riders[i].localRotation=Quaternion.Euler(-input.brake*8,0,(i==3?-displayedBalance*40:input.steer*5)+Mathf.Sin(s.elapsed*4+i)*s.speed*.06f);
        }
        private void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);}
    }
}
