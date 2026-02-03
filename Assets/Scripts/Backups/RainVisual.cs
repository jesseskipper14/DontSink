//using UnityEngine;

//public class RainVisual : MonoBehaviour
//{
//    public Transform boatTransform;
//    public float maxHeight = 10f;

//    private void Update()
//    {
//        if (WeatherManager.Instance == null || boatTransform == null) return;

//        float intensity = WeatherManager.Instance.rainIntensity;

//        // For debugging: draw lines from above the boat down
//        int drops = Mathf.CeilToInt(20 * intensity);
//        for (int i = 0; i < drops; i++)
//        {
//            float xOffset = Random.Range(-boatTransform.localScale.x * 0.5f, boatTransform.localScale.x * 0.5f);
//            Vector3 start = boatTransform.position + new Vector3(xOffset, maxHeight, 0f);
//            Vector3 end = boatTransform.position + new Vector3(xOffset, 0f, 0f);

//            Debug.DrawLine(start, end, Color.cyan);
//        }
//    }
//}
