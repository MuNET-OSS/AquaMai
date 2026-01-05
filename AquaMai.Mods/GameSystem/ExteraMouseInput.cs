using AquaMai.Config.Attributes;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AquaMai.Mods.GameSystem.ExclusiveTouch;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "触屏优化",
    defaultOn: true,
    en: "Enable touch input with radius for a more realistic touchscreen experience.",
    zh: "启用触摸输入和半径，以获得更真实的触摸屏体验")]
public partial class ExteraMouseInput
{
    [ConfigEntry(
        name: "触摸半径",
        en: "Touch area radius size. Adjust according to the size of your finger, you can test in Test Mode.",
        zh: "触摸区域半径大小。请根据手指大小调整，可以去 Test 中测试")]
    public readonly static float radius = 25;

    [ConfigEntry(
        name: "显示触摸点",
        en: "Display touch points (only for touch input).",
        zh: "显示触摸点（仅限触屏输入）")]
    public readonly static bool displayArea = false;

    static RectTransform[] rectTransform = new RectTransform[2];
    static List<CustomCircleGraphic> circleGraphics = new List<CustomCircleGraphic>();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MouseTouchPanel), "Start")]
    public static void MouseTouchPanelStart(MouseTouchPanel __instance)
    {
        var player = __instance.transform.parent.parent.name.Equals("LeftMonitor") ? 0 : 1;
        UnityEngine.Object.Destroy(__instance.transform.parent.GetComponent<GraphicRaycaster>());
        __instance.transform.parent.gameObject.AddComponent<MeshButtonRaycaster>();
        rectTransform[player] = __instance.GetComponent<RectTransform>();

        if (displayArea && player == 0)
        {
            __instance.StartCoroutine(UpdateInputDisplay());
            circleGraphics = new List<CustomCircleGraphic>(10);
            for (int i = 0; i < 10; i++)
            {
                GameObject go = new GameObject("CircleGraphic" + i);
                go.transform.SetParent(rectTransform[0]);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                CustomCircleGraphic circleGraphic = go.AddComponent<CustomCircleGraphic>();
                circleGraphic.color = new Color(1, 1, 1, 0.5f);
                circleGraphic.raycastTarget = false;
                go.gameObject.SetActive(false);
                circleGraphics.Add(circleGraphic);
            }
        }
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MeshButton), "Awake")]
    public static void MeshButtonAwake(MeshButton __instance, ref Vector2[] ___vertexArray)
    {
        RectTransform mouseTouchPanelRect = __instance.transform.parent.GetComponent<MouseTouchPanel>().GetComponent<RectTransform>();
        CustomGraphic customGraphic = __instance.targetGraphic as CustomGraphic;
        ___vertexArray = new Vector2[customGraphic.vertex.Count];
        for (int i = 0; i < customGraphic.vertex.Count; i++)
        {
            var localPos3 = new Vector3(customGraphic.vertex[i].x, customGraphic.vertex[i].y, 0f);
            var rotatedPos3 = __instance.transform.localRotation * localPos3;             //ApplyRot
            var scaledPos3 = Vector3.Scale(rotatedPos3, __instance.transform.localScale); // ApplyScale
            var finalPos3 = __instance.transform.position + scaledPos3;                   //ApplyPos

            ___vertexArray[i] = RectTransformUtility.WorldToScreenPoint(
                Camera.main,
                finalPos3);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mouseTouchPanelRect,
                ___vertexArray[i],
                Camera.main,
                out ___vertexArray[i]
            );
        }
    }

    //显示输入范围
    static IEnumerator UpdateInputDisplay()
    {
        while (true)
        {
            for (int i = 0; i < circleGraphics.Count; i++)
            {
                if (Input.touchCount > i)
                {
                    Vector2 touchPoint = Input.GetTouch(i).position;
                    Vector2 localPoint;

                    // 将屏幕点转换为本地空间
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform[0], touchPoint, Camera.main, out localPoint);
                    circleGraphics[i].rectTransform.anchoredPosition = localPoint;
                    // 缩放半径
                    circleGraphics[i].radius = radius;
                    circleGraphics[i].gameObject.SetActive(true);
                }
                else
                {
                    circleGraphics[i].gameObject.SetActive(false);
                }
            }

            yield return null;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MeshButton), "IsPointInPolygon", new Type[] { typeof(Vector2[]), typeof(Vector2) })]
    public static bool IsPointInPolygon(MeshButton __instance, Vector2[] polygon, Vector2 point, ref bool __result)
    {
        var player = __instance.transform.parent.parent.parent.name.Equals("LeftMonitor") ? 0 : 1;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform[player],
            point,
            Camera.main,
            out var localInputPoint
        );

        // 防止触发另外半边的
        if (localInputPoint.x * 2 is > 1080 or < -1080)
        {
            __result = false;
            return false;
        }

        bool isInsidePolygon = false;
        //检查是否在多边形顶点内
        isInsidePolygon = PolygonRaycasting.IsVertDistance(polygon, localInputPoint, radius);
        //检查是否在多边形边上
        if (!isInsidePolygon)
        {
            isInsidePolygon = PolygonRaycasting.IsCircleIntersectingPolygonEdges(polygon, localInputPoint, radius);
        }
        // 检查是否在多边形内部
        if (!isInsidePolygon)
        {
            isInsidePolygon = PolygonRaycasting.InPointInInternal(polygon, localInputPoint);
        }
        //if (isInsidePolygon)
        //{
        //    foreach (var p in polygon)
        //    {
        //        var testo = new GameObject("A");
        //        testo.transform.SetParent(rectTransform);
        //        testo.transform.localPosition = p;
        //        testo.transform.localScale = Vector3.one * 0.2f;
        //        testo.AddComponent<RectTransform>();
        //        testo.AddComponent<Image>();
        //    }
        //}
        __result = isInsidePolygon;
        return false;
    }

    #region ⚪

    public class CustomCircleGraphic : Graphic
    {
        public float radius = 50f;    // 圆的半径
        public int segmentCount = 64; // 圆的细分段数
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            List<UIVertex> vertices = new List<UIVertex>();
            List<int> indices = new List<int>();

            // 添加圆心点
            UIVertex centerVertex = UIVertex.simpleVert;
            centerVertex.position = Vector3.zero; // 圆心
            centerVertex.color = this.color;
            vertices.Add(centerVertex);

            // 添加圆周点
            for (int i = 0; i <= segmentCount; i++) // 多加一个点，形成闭合圆
            {
                float angle = Mathf.Deg2Rad * (i * 360f / segmentCount);
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;

                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(x, y, 0f);
                vertex.color = this.color;
                vertices.Add(vertex);

                if (i > 0)
                {
                    // 形成三角形的索引
                    indices.Add(0);     // 圆心
                    indices.Add(i);     // 当前点
                    indices.Add(i - 1); // 上一个点
                }
            }

            vh.AddUIVertexStream(vertices, indices);
        }
    }
    public class MeshButtonRaycaster : BaseRaycaster
    {
        public override Camera eventCamera => Camera.main;

        MeshButton[] meshButtons = new MeshButton[0];

        protected override void Start()
        {
            meshButtons = transform.GetComponentsInChildren<MeshButton>(true);
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            foreach (var item in meshButtons)
            {
                // 检查按钮是否实现了 ICanvasRaycastFilter，并调用过滤逻辑
                var raycastFilter = item as ICanvasRaycastFilter;
                if (!raycastFilter.IsRaycastLocationValid(eventData.position, eventCamera))
                {
                    continue; // 如果过滤不通过，跳过这个按钮
                }
            }
        }
    }

    #endregion
}