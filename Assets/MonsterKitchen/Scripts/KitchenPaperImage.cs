using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterKitchen
{
    /// <summary>Small authored paper silhouettes; no generated bitmap artwork.</summary>
    public sealed class KitchenPaperImage : Image
    {
        public float Corner=14;
        public bool Torn;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r=GetPixelAdjustedRect();
            float radius=Mathf.Min(Corner,Mathf.Min(r.width,r.height)*.5f);
            var points=new List<Vector2>();
            for(int corner=0;corner<4;corner++)
            {
                float cx=corner<2?r.xMax-radius:r.xMin+radius;
                float cy=corner==0||corner==3?r.yMax-radius:r.yMin+radius;
                for(int j=0;j<=6;j++)
                {
                    float a=(90-corner*90-j*15)*Mathf.Deg2Rad;
                    points.Add(new Vector2(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius));
                }
                if(corner==1&&Torn)
                {
                    int teeth=Mathf.Max(2,Mathf.FloorToInt((r.width-radius*2)/18));
                    for(int k=1;k<teeth;k++)points.Add(new Vector2(Mathf.Lerp(r.xMax-radius,r.xMin+radius,k/(float)teeth),r.yMin+(k%2==0?0:5)));
                }
            }
            mesh.AddVert(r.center,color,Vector2.zero);
            foreach(var p in points)mesh.AddVert(p,color,Vector2.zero);
            for(int i=0;i<points.Count;i++)mesh.AddTriangle(0,i+1,(i+1)%points.Count+1);
        }
    }
}
