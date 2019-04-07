using System.Collections.Generic;
using UnityEngine;

public class SkinnedMeshBaker : MonoBehaviour {

    [SerializeField] SkinnedMeshRenderer source = null;
    [SerializeField] RenderTexture positionMap = null;
    [SerializeField] RenderTexture velocityMap = null;
    [SerializeField] RenderTexture normalMap = null;
    [SerializeField] ComputeShader compute = null;

    Mesh mesh;
    int[] dimensions = new int[2];

    List<Vector3> positionList = new List<Vector3>();
    List<Vector3> normalList = new List<Vector3>();

    ComputeBuffer positionBuffer1;
    ComputeBuffer positionBuffer2;
    ComputeBuffer normalBuffer;

    RenderTexture tempPositionMap;
    RenderTexture tempVelocityMap;
    RenderTexture tempNormalMap;

    private bool warned;

    void Start() {
        mesh = new Mesh();
    }

    void OnDestroy() {
        Destroy(mesh);
        mesh = null;

        Utility.TryDispose(positionBuffer1);
        Utility.TryDispose(positionBuffer2);
        Utility.TryDispose(normalBuffer);

        Utility.TryDestroy(tempPositionMap);
        Utility.TryDestroy(tempVelocityMap);
        Utility.TryDestroy(tempNormalMap);
    }

    void Update() {
        if (source == null) return;

        source.BakeMesh(mesh);
        mesh.GetVertices(positionList);
        mesh.GetNormals(normalList);

        if (!CheckConsistency()) return;
        TransferData();
        Utility.SwapBuffer(ref positionBuffer1, ref positionBuffer2);
    }

    void TransferData() {
        var mapWidth = positionMap.width;
        var mapHeight = positionMap.height;
        Debug.Log("width: " + mapWidth);
        Debug.Log("height: " + mapHeight);

        var vcount = positionList.Count;
        var vcount_x3 = vcount * 3;
        Debug.Log(vcount);

        // Release the temporary objects when the size of them don't match the input.
        if (positionBuffer1 != null && positionBuffer1.count != vcount_x3) {
            positionBuffer1 = null;
            positionBuffer2 = null;
            normalBuffer = null;
        }
        if (tempPositionMap != null && (tempPositionMap.width != mapWidth || tempPositionMap.height != mapHeight)) {
            tempPositionMap = null;
            tempVelocityMap = null;
            tempNormalMap = null;
        }

        // Lazy initialization of temporary object
        if (positionBuffer1 == null) {
            positionBuffer1 = new ComputeBuffer(vcount_x3, sizeof(float));
            positionBuffer2 = new ComputeBuffer(vcount_x3, sizeof(float));
            normalBuffer = new ComputeBuffer(vcount_x3, sizeof(float));
        }
        if (tempPositionMap == null) {
            Debug.Log("tempPositionMap == null");
            tempPositionMap = Utility.CreateRenderTexture(mapWidth, mapHeight);
            tempVelocityMap = Utility.CreateRenderTexture(mapWidth, mapHeight);
            tempNormalMap = Utility.CreateRenderTexture(mapWidth, mapHeight);
        }


        // Set data and execute the transfer task.
        compute.SetInt("VertexCount", vcount);
        compute.SetMatrix("Transform", source.transform.localToWorldMatrix);
        compute.SetFloat("FrameRate", 1 / Time.deltaTime);

        positionBuffer1.SetData(positionList);
        normalBuffer.SetData(normalList);

        compute.SetBuffer(0, "PositionBuffer", positionBuffer1);
        compute.SetBuffer(0, "OldPositionBuffer", positionBuffer2);
        compute.SetBuffer(0, "NormalBuffer", normalBuffer);
        compute.SetTexture(0, "PositionMap", tempPositionMap);
        compute.SetTexture(0, "VelocityMap", tempVelocityMap);
        compute.SetTexture(0, "NormalMap", tempNormalMap);
        compute.Dispatch(0, mapWidth / 8, mapHeight / 8, 1);

        Graphics.CopyTexture(tempPositionMap, positionMap);
        Graphics.CopyTexture(tempVelocityMap, velocityMap);
        Graphics.CopyTexture(tempNormalMap, normalMap);
    }

    bool CheckConsistency() {
        if (warned) return false;
        if (positionMap.width % 8 != 0 || positionMap.height % 8 != 0) {
            Debug.LogError("Position map dimensions should be a multiple of 8.");
            warned = true;
        }
        if (normalMap.width != positionMap.width ||
            normalMap.height != positionMap.height) {
            Debug.LogError("Position/normal map dimensions should match.");
            warned = true;
        }
        if (positionMap.format != RenderTextureFormat.ARGBHalf &&
            positionMap.format != RenderTextureFormat.ARGBFloat) {
            Debug.LogError("Position map format should be ARGBHalf or ARGBFloat.");
            warned = true;
        }
        if (normalMap.format != RenderTextureFormat.ARGBHalf &&
            normalMap.format != RenderTextureFormat.ARGBFloat) {
            Debug.LogError("Normal map format should be ARGBHalf or ARGBFloat.");
            warned = true;
        }
        return !warned;
    }
}

internal static class Utility {
    public static RenderTexture CreateRenderTexture(int width, int height) {
        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    public static void TryDispose(System.IDisposable obj) {
        if (obj == null) return;
        obj.Dispose();
        obj = null;
    }

    public static void TryDestroy(UnityEngine.Object obj) {
        if (obj == null) return;
        UnityEngine.Object.Destroy(obj);
    }

    public static void SwapBuffer(ref ComputeBuffer b1, ref ComputeBuffer b2) {
        var temp = b1;
        b1 = b2;
        b2 = temp;
    }
}