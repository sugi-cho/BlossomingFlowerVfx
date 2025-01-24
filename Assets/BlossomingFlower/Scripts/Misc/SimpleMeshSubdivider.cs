using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimpleMeshSubdivider : MonoBehaviour
{
    [SerializeField] List<Mesh> meshes;
    [SerializeField] int subdivideCount = 4;

    // [ContextMenu] を付けると、インスペクタの右上メニューから実行できるようになります。
    [ContextMenu("Subdivide")]
    public void SubdivideMesh()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf != null)
            meshes = new List<Mesh> { mf.mesh };

        meshes.ForEach(mesh =>
        {
            for (var i = 0; i < subdivideCount; i++)
            {
                Subdivide(mesh);
            }
        });
    }

#if UNITY_EDITOR
    [ContextMenu("save mesh")]
    void SaveMesh()
    {
        meshes.ForEach(mesh =>
        {
            AssetDatabase.CreateAsset(mesh, $"Assets/{mesh.name}.mesh");
        });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
#endif

    /// <summary>
    /// 与えられた Mesh を1回分だけサブディビジョン化し、新しい Mesh を返す。
    /// </summary>
    private void Subdivide(Mesh mesh)
    {
        // 必要な配列を取得
        Vector3[] oldVerts = mesh.vertices;
        Vector3[] oldNormals = mesh.normals;
        Vector2[] oldUVs = mesh.uv;
        int[] oldTris = mesh.triangles;

        // 新規頂点・三角形リスト
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
        List<int> newTris = new List<int>();

        // 元のメッシュ上の全頂点を最初にコピー
        // （あとから中点を追加していく形）
        for (int i = 0; i < oldVerts.Length; i++)
        {
            newVerts.Add(oldVerts[i]);
            if (oldNormals.Length > i) newNormals.Add(oldNormals[i]);
            if (oldUVs.Length > i) newUVs.Add(oldUVs[i]);
        }

        // エッジを識別するための Dictionary
        // key: (小さい方の頂点Index << 32) | 大きい方の頂点Index
        // value: 新規に生成した中点頂点のIndex
        Dictionary<long, int> edgeMidpointIndex = new Dictionary<long, int>();

        // 中点を返す関数（存在しない場合は作る）
        System.Func<int, int, int> GetOrCreateMidPoint = (indexA, indexB) =>
        {
            // ソートして key を作成
            long key = (indexA < indexB)
                        ? ((long)indexA << 32) + (long)indexB
                        : ((long)indexB << 32) + (long)indexA;

            if (edgeMidpointIndex.TryGetValue(key, out int existingMidIndex))
            {
                // 既に作成済みならそれを返す
                return existingMidIndex;
            }

            // 新しい中点を作成
            Vector3 v = (newVerts[indexA] + newVerts[indexB]) * 0.5f;
            newVerts.Add(v);

            if (newNormals.Count > 0)
            {
                Vector3 n = (newNormals[indexA] + newNormals[indexB]).normalized;
                newNormals.Add(n);
            }

            if (newUVs.Count > 0)
            {
                Vector2 uv = (newUVs[indexA] + newUVs[indexB]) * 0.5f;
                newUVs.Add(uv);
            }

            int newIndex = newVerts.Count - 1;
            edgeMidpointIndex.Add(key, newIndex);
            return newIndex;
        };

        // 元の三角形を4つの三角形に分割
        for (int i = 0; i < oldTris.Length; i += 3)
        {
            int i1 = oldTris[i];
            int i2 = oldTris[i + 1];
            int i3 = oldTris[i + 2];

            // エッジの中点を取得（生成）
            int m12 = GetOrCreateMidPoint(i1, i2);
            int m23 = GetOrCreateMidPoint(i2, i3);
            int m31 = GetOrCreateMidPoint(i3, i1);

            // 4つの三角形を構成
            // 1) (i1, m12, m31)
            newTris.Add(i1);
            newTris.Add(m12);
            newTris.Add(m31);

            // 2) (m12, i2, m23)
            newTris.Add(m12);
            newTris.Add(i2);
            newTris.Add(m23);

            // 3) (m31, m23, i3)
            newTris.Add(m31);
            newTris.Add(m23);
            newTris.Add(i3);

            // 4) (m12, m23, m31)
            newTris.Add(m12);
            newTris.Add(m23);
            newTris.Add(m31);
        }

        mesh.vertices = newVerts.ToArray();
        mesh.triangles = newTris.ToArray();

        // 新しい頂点数に応じてノーマル/UVもセット
        if (newNormals.Count == newVerts.Count)
        {
            mesh.normals = newNormals.ToArray();
        }
        else
        {
            mesh.RecalculateNormals();
        }
        if (newUVs.Count == newVerts.Count)
        {
            mesh.uv = newUVs.ToArray();
        }

        mesh.RecalculateBounds();
        // 必要に応じてタンジェント計算など
        // newMesh.RecalculateTangents();
    }
}
