using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MonsterKitchen.Editor
{
    public static class CookbookSceneBuilder
    {
        const string Root="Assets/MonsterKitchen";
        static Transform props;
        static Material paper,edge,cover,ring,clothA,clothB,wood;
        [MenuItem("MonsterKitchen/Build cookbook title")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first.");
            paper=Mat("Book paper","F8EBCB");edge=Mat("Page edges","DDCAA7");
            cover=Mat("Cookbook cover","6C473A");ring=Mat("Copper rings","A64E29",.5f);
            clothA=Mat("Tablecloth orange","DFA260");clothB=Mat("Tablecloth cream","F5DFAF");wood=Mat("Warm table wood","885B3E");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);scene.name="Title";
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.86f,.86f,.78f);
            RenderSettings.ambientEquatorColor=new Color(.62f,.60f,.53f);
            RenderSettings.ambientGroundColor=new Color(.36f,.28f,.23f);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(.3f,13,-8.6f);camera.transform.LookAt(new Vector3(0,0,.15f));
            camera.orthographic=true;camera.orthographicSize=5.5f;camera.nearClipPlane=.1f;camera.farClipPlane=50;
            camera.backgroundColor=new Color(.45f,.29f,.17f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var sunlight=new GameObject("Window sunlight").AddComponent<Light>();sunlight.type=LightType.Directional;
            sunlight.transform.rotation=Quaternion.Euler(58,-45,0);sunlight.intensity=1.15f;sunlight.color=new Color(1,.96f,.88f);
            sunlight.shadows=LightShadows.Soft;sunlight.shadowStrength=.65f;
            props=new GameObject("Kitchen tabletop").transform;
            Cube("Solid wooden tabletop",props,new Vector3(0,-.22f,0),new Vector3(22,.35f,17),wood);
            for(int x=-9;x<10;x++)for(int z=-8;z<9;z++)Cube("Tablecloth square",props,new Vector3(x*1.1f,-.015f,z*1.1f),new Vector3(1.1f,.04f,1.1f),(x+z)%2==0?clothA:clothB);
            var book=new GameObject("Open recipe book").transform;book.rotation=Quaternion.Euler(0,-5,0);
            Cube("Leather cover",book,new Vector3(0,.11f,0),new Vector3(10.65f,.19f,7.5f),cover);
            Cube("Left page block",book,new Vector3(-2.63f,.23f,0),new Vector3(5.04f,.14f,7.13f),edge);
            Cube("Right page block",book,new Vector3(2.63f,.23f,0),new Vector3(5.04f,.14f,7.13f),edge);
            // Visible page laminations and subtly bowed surfaces give the book real volume.
            for(int i=0;i<5;i++)
            {
                Cube("Left page edge",book,new Vector3(-2.63f,.18f+i*.024f,-3.57f),new Vector3(5.02f,.008f,.014f),paper);
                Cube("Right page edge",book,new Vector3(2.63f,.18f+i*.024f,-3.57f),new Vector3(5.02f,.008f,.014f),paper);
            }
            Page(book,-1);Page(book,1);
            Cube("Binder spine",book,new Vector3(0,.35f,0),new Vector3(.13f,.12f,7.15f),cover);
            for(int i=0;i<7;i++)
            {
                float z=-2.85f+i*.95f;
                var ringObject=new GameObject("Copper binder ring "+i,typeof(MeshFilter),typeof(MeshRenderer));ringObject.transform.SetParent(book,false);
                ringObject.transform.localPosition=new Vector3(0,.47f,z);
                ringObject.GetComponent<MeshFilter>().sharedMesh=RingMesh(i);
                ringObject.GetComponent<MeshRenderer>().sharedMaterial=ring;
            }
            var tabColors=new[]{Mat("Recipe tab sage","819C74"),Mat("Recipe tab tomato","BD5E49"),Mat("Recipe tab plum","745F73")};
            for(int i=0;i<3;i++)Cube("Recipe bookmark",book,new Vector3(5.25f,.21f,1.8f-i*1.3f),new Vector3(.55f,.035f,.7f),tabColors[i]);

            // Real kitchen props around the page instead of decorative interface cards.
            Model("cuttingboard",new Vector3(7.45f,.04f,2.35f),2.6f,18);
            Model("food_ingredient_tomato",new Vector3(7.15f,.24f,2.65f),.67f,0);
            Model("knife",new Vector3(7.6f,.23f,1.92f),1.25f,18);
            Model("pan_A",new Vector3(-7.15f,.04f,3.4f),2.8f,-18);
            Model("food_ingredient_burger_cooked",new Vector3(-7.28f,.27f,3.3f),.87f,0);
            Model("jar_A_large",new Vector3(6.35f,.04f,-3.7f),.8f,0);
            Model("jar_B_large",new Vector3(7.3f,.04f,-3.45f),.7f,0);
            Model("plate",new Vector3(-7.15f,.05f,-2.6f),2.15f,0);
            Model("food_burger",new Vector3(-7.15f,.20f,-2.6f),1.3f,-12);
            var systems=new GameObject("Menu systems");systems.AddComponent<GameSession>().IsTitleScene=true;
            var audio=systems.AddComponent<AudioFeedback>();audio.CueNames=new[]{"click","confirm","back"};
            audio.Clips=new[]{KitchenAssetSetup.Sound("ui-click"),KitchenAssetSetup.Sound("ui-confirm"),KitchenAssetSetup.Sound("ui-back")};
            audio.Ambience=KitchenAssetSetup.Sound("ambience-kitchen");
            var menu=systems.AddComponent<CookbookMenu>();menu.Book=book;menu.MenuCamera=camera;
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/Title.unity");AssetDatabase.SaveAssets();
            return "Replaced title with a physical cookbook menu and tabletop scene.";
        }
        static void Page(Transform book,int side)
        {
            const int steps=20;var vertices=new Vector3[(steps+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[steps*6];
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps;float x=side*(.14f+t*4.99f),y=.36f+.09f*Mathf.Exp(-t*10);
                vertices[i*2]=new Vector3(x,y,-3.55f);vertices[i*2+1]=new Vector3(x,y,3.55f);
                uv[i*2]=new Vector2(t,0);uv[i*2+1]=new Vector2(t,1);
                if(i<steps){int p=i*6,a=i*2; if(side==1){triangles[p]=a;triangles[p+1]=a+1;triangles[p+2]=a+2;triangles[p+3]=a+2;triangles[p+4]=a+1;triangles[p+5]=a+3;}else{triangles[p]=a;triangles[p+1]=a+2;triangles[p+2]=a+1;triangles[p+3]=a+2;triangles[p+4]=a+3;triangles[p+5]=a+1;}}
            }
            var mesh=new Mesh{name=side<0?"Left curved page":"Right curved page"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Root+"/Generated/"+mesh.name+".asset";AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(book,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=paper;
        }
        static Mesh RingMesh(int index)
        {
            const int around=40,tube=8;var v=new Vector3[(around+1)*(tube+1)];var triangles=new int[around*tube*6];
            for(int i=0;i<=around;i++)for(int j=0;j<=tube;j++)
            {
                float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/tube;
                v[i*(tube+1)+j]=new Vector3((.31f+.055f*Mathf.Cos(b))*Mathf.Cos(a),(.23f+.055f*Mathf.Cos(b))*Mathf.Sin(a),.055f*Mathf.Sin(b));
                if(i<around&&j<tube){int k=(i*tube+j)*6,p=i*(tube+1)+j;triangles[k]=p;triangles[k+1]=p+tube+1;triangles[k+2]=p+1;triangles[k+3]=p+1;triangles[k+4]=p+tube+1;triangles[k+5]=p+tube+2;}
            }
            var mesh=new Mesh{name="Book ring "+index};mesh.vertices=v;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Root+"/Generated/BookRing"+index+".asset";AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void Model(string name,Vector3 pos,float width,float yaw)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(KitchenAssetSetup.Model(name),props);
            // Rotate before measuring so the requested width and centre describe the
            // final footprint, including a pan handle or a tilted cutting board.
            go.transform.position=pos;
            go.transform.localRotation=Quaternion.Euler(0,yaw,0)*(name=="knife"?Quaternion.Euler(0,0,90):Quaternion.identity);
            var b=KitchenAssets.BoundsOf(go);go.transform.localScale*=width/Mathf.Max(b.size.x,b.size.z);
            b=KitchenAssets.BoundsOf(go);go.transform.position+=pos-new Vector3(b.center.x,b.min.y,b.center.z);
        }
        static void Cube(string name,Transform parent,Vector3 pos,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        }
        static Material Mat(string name,string hex,float smooth=.1f)
        {
            string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            ColorUtility.TryParseHtmlString("#"+hex,out var color);material.color=color;material.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(material);return material;
        }
    }
}
