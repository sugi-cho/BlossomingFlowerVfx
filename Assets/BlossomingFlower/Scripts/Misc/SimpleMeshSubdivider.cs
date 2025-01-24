using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimpleMeshSubdivider : MonoBehaviour
{
    [SerializeField] List<Mesh> meshes;
    [SerializeField] int subdivideCount = 4;

    // [ContextMenu] ��t����ƁA�C���X�y�N�^�̉E�チ�j���[������s�ł���悤�ɂȂ�܂��B
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
    /// �^����ꂽ Mesh ��1�񕪂����T�u�f�B�r�W���������A�V���� Mesh ��Ԃ��B
    /// </summary>
    private void Subdivide(Mesh mesh)
    {
        // �K�v�Ȕz����擾
        Vector3[] oldVerts = mesh.vertices;
        Vector3[] oldNormals = mesh.normals;
        Vector2[] oldUVs = mesh.uv;
        int[] oldTris = mesh.triangles;

        // �V�K���_�E�O�p�`���X�g
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
        List<int> newTris = new List<int>();

        // ���̃��b�V����̑S���_���ŏ��ɃR�s�[
        // �i���Ƃ��璆�_��ǉ����Ă����`�j
        for (int i = 0; i < oldVerts.Length; i++)
        {
            newVerts.Add(oldVerts[i]);
            if (oldNormals.Length > i) newNormals.Add(oldNormals[i]);
            if (oldUVs.Length > i) newUVs.Add(oldUVs[i]);
        }

        // �G�b�W�����ʂ��邽�߂� Dictionary
        // key: (���������̒��_Index << 32) | �傫�����̒��_Index
        // value: �V�K�ɐ����������_���_��Index
        Dictionary<long, int> edgeMidpointIndex = new Dictionary<long, int>();

        // ���_��Ԃ��֐��i���݂��Ȃ��ꍇ�͍��j
        System.Func<int, int, int> GetOrCreateMidPoint = (indexA, indexB) =>
        {
            // �\�[�g���� key ���쐬
            long key = (indexA < indexB)
                        ? ((long)indexA << 32) + (long)indexB
                        : ((long)indexB << 32) + (long)indexA;

            if (edgeMidpointIndex.TryGetValue(key, out int existingMidIndex))
            {
                // ���ɍ쐬�ς݂Ȃ炻���Ԃ�
                return existingMidIndex;
            }

            // �V�������_���쐬
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

        // ���̎O�p�`��4�̎O�p�`�ɕ���
        for (int i = 0; i < oldTris.Length; i += 3)
        {
            int i1 = oldTris[i];
            int i2 = oldTris[i + 1];
            int i3 = oldTris[i + 2];

            // �G�b�W�̒��_���擾�i�����j
            int m12 = GetOrCreateMidPoint(i1, i2);
            int m23 = GetOrCreateMidPoint(i2, i3);
            int m31 = GetOrCreateMidPoint(i3, i1);

            // 4�̎O�p�`���\��
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

        // �V�������_���ɉ����ăm�[�}��/UV���Z�b�g
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
        // �K�v�ɉ����ă^���W�F���g�v�Z�Ȃ�
        // newMesh.RecalculateTangents();
    }
}
