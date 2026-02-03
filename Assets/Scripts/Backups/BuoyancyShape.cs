//using UnityEngine;
//[ExecuteAlways]
//public class BuoyancyShape : MonoBehaviour
//{
//    public float width = 1f;
//    public float height = 1f;
//    public float volume = 1f;
//    public int debugSliceCount = 10;
//    public bool debugBuoyancy = false;

//    void OnValidate()
//    {
//        transform.localScale = new Vector3(width, height, 1f);
//        volume = width * height;
//    }
//    void LateUpdate()
//    {
//        transform.localScale = new Vector3(width, height, 1f);
//    }
//    private void OnDrawGizmos()
//    {
//        if (!debugBuoyancy) return;

//        // Draw the main bounding box
//        Gizmos.color = Color.cyan;
//        Vector3 center = transform.position;
//        Vector3 size = new Vector3(width, height, 0.01f);
//        Gizmos.DrawWireCube(center, size);

//        // Draw slices
//        if (debugSliceCount <= 0) return;
//        Gizmos.color = Color.yellow;
//        float sliceWidth = width / debugSliceCount;
//        float leftX = transform.position.x - width * 0.5f;

//        for (int i = 0; i < debugSliceCount; i++)
//        {
//            float sliceX = leftX + sliceWidth * (i + 0.5f);
//            Vector3 top = new Vector3(sliceX, transform.position.y + height * 0.5f, transform.position.z);
//            Vector3 bottom = new Vector3(sliceX, transform.position.y - height * 0.5f, transform.position.z);
//            Gizmos.DrawLine(top, bottom);
//        }
//    }
//}


