using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ratones.Basic
{
    // Geometría provisional: las alturas y colisiones usan ArenaLayout en todas las copias.
    public sealed class ArenaView : MonoBehaviour
    {
        readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
        public static void Ensure()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponentInChildren<ArenaView>(true) != null) return;
            Create();
        }
        public static ArenaView Create(Material floor = null, Material blocks = null,
            Func<string, Mesh, Mesh> persistMesh = null)
        {
            var view = new GameObject("RampasYObstaculos").AddComponent<ArenaView>();
            if (floor == null) floor = view.Material(new Color(.66f,.66f,.66f));
            if (blocks == null) blocks = view.Material(new Color(.38f,.38f,.38f));
            int number = 0;
            foreach (RaisedArea area in ArenaLayout.Platforms)
            {
                number++;
                float yaw = area.AlongX ? 90 : 0;
                Cube(view.transform,"Plataforma" + number, new Vector3(area.X,area.Height/2,area.Z),
                    new Vector3(area.Width,area.Height,area.FlatLength),yaw,floor);
                Mesh mesh = Wedge(area.Width,area.RampLength,area.Height);
                mesh.name = "Rampa" + number;
                if (persistMesh != null) mesh = persistMesh(mesh.name,mesh);
                else view.temporary.Add(mesh);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float offset = sign*(area.FlatLength+area.RampLength)/2;
                    var ramp = new GameObject("Rampa" + number + (sign < 0 ? "A" : "B"));
                    ramp.transform.SetParent(view.transform,false);
                    ramp.transform.localPosition = new Vector3(area.X+(area.AlongX?offset:0),0,area.Z+(area.AlongX?0:offset));
                    ramp.transform.localRotation = Quaternion.Euler(0,yaw+(sign > 0 ? 180 : 0),0);
                    ramp.AddComponent<MeshFilter>().sharedMesh = mesh;
                    ramp.AddComponent<MeshRenderer>().sharedMaterial = floor;
                    ramp.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            }
            number = 0;
            foreach (ArenaBlock block in ArenaLayout.Blocks)
                Cube(view.transform,"Obstaculo" + (++number),new Vector3(block.X,block.Height/2,block.Z),
                    new Vector3(block.Width,block.Height,block.Depth),0,blocks);
            return view;
        }
        Material Material(Color color)
        {
            var material = new Material(Shader.Find("Standard")); material.color=color;
            temporary.Add(material); return material;
        }
        static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, float yaw, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name=name;
            cube.transform.SetParent(parent,false); cube.transform.localPosition=position;
            cube.transform.localScale=scale; cube.transform.localRotation=Quaternion.Euler(0,yaw,0);
            cube.GetComponent<Renderer>().sharedMaterial=material;
        }
        static Mesh Wedge(float width, float length, float height)
        {
            float w=width/2, l=length/2;
            var mesh = new Mesh();
            mesh.vertices = new[] {
                new Vector3(-w,0,-l),new Vector3(w,0,-l),new Vector3(-w,0,l),
                new Vector3(w,0,l),new Vector3(-w,height,l),new Vector3(w,height,l)
            };
            mesh.triangles = new[] {0,1,2, 1,3,2, 0,4,1, 1,4,5, 0,2,4, 1,5,3, 2,3,4, 3,5,4};
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        void OnDestroy()
        {
            foreach (UnityEngine.Object resource in temporary)
                if (resource != null) { if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource); }
        }
    }
}
