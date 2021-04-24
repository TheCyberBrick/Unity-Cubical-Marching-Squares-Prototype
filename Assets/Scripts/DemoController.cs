using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxel;

public class DemoController : MonoBehaviour
{
    public VoxelWorldContainer world;
    public SdfShapeRenderHandler shapeRenderer;
    public CreateVoxelTerrain terrain;

    public Material matUnion, matDiff;

    private bool renderSdfUnion;
    private ISdf renderSdf;
    private Matrix4x4 renderSdfTransform;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Behaviour());
    }

    // Update is called once per frame
    void Update()
    {
        if (renderSdf != null)
        {
            shapeRenderer.Render(renderSdfTransform, renderSdf, renderSdfUnion ? matUnion : matDiff);
        }
    }

    private IEnumerator Behaviour()
    {
        yield return new WaitForSeconds(1);

        renderSdfUnion = true;
        renderSdf = new BoxSDF(9.8f);
        renderSdfTransform = Matrix4x4.Translate(new Vector3(0, 10, 0)) * Matrix4x4.Rotate(Quaternion.Euler(-1, 0, -1));

        yield return new WaitForSeconds(1);

        renderSdf = null;
        world.Instance.ApplySdf<BoxSDF>(new Vector3(0, 10, 0), Quaternion.Euler(-1, 0, -1), new BoxSDF(10), MaterialColors.ToInteger(255, 255, 255, 0), false, null);

        yield return new WaitForSeconds(1);

        renderSdfUnion = true;
        renderSdf = new SphereSDF(9.8f);
        renderSdfTransform = Matrix4x4.Translate(new Vector3(5, 14, 8));

        yield return new WaitForSeconds(1);

        renderSdf = null;
        world.Instance.ApplySdf<SphereSDF>(new Vector3(5, 14, 8), Quaternion.identity, new SphereSDF(10), MaterialColors.ToInteger(255, 255, 255, 1), false, null);

        yield return new WaitForSeconds(1);

        renderSdfUnion = false;
        renderSdf = new CylinderSDF(9, 8);
        renderSdfTransform = Matrix4x4.Translate(new Vector3(-3.5f, 14, 16.5f));

        yield return new WaitForSeconds(1);

        renderSdf = null;
        world.Instance.ApplySdf<CylinderSDF>(new Vector3(-3.5f, 14, 16.5f), Quaternion.identity, new CylinderSDF(8, 8), 0, false, null);

        yield return new WaitForSeconds(1);

        Vector3 start = new Vector3(15, 20, 15);
        Vector3 end = new Vector3(-15, 20, -15);

        for (int i = 0; i < 200; i++)
        {
            Vector3 curr = start + (end - start) / 200.0f * i;

            renderSdfUnion = true;
            renderSdf = new BoxSDF(3.123f);
            renderSdfTransform = Matrix4x4.Translate(curr) * Matrix4x4.Rotate(Quaternion.Euler(i / 200.0f * 90.0f, i / 200.0f * 360.0f, 0));

            world.Instance.ApplySdf<BoxSDF>(curr, Quaternion.Euler(i / 200.0f * 90.0f, i / 200.0f * 360.0f, 0), new BoxSDF(3.123f), MaterialColors.ToInteger(255, 255, 255, 2), true, null);

            yield return new WaitForSeconds(0.005f);
        }
    }
}
