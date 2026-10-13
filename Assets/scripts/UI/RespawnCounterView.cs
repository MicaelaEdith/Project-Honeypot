using UnityEngine;

[DisallowMultipleComponent]
public class RespawnCounterView : MonoBehaviour
{
    [SerializeField] private GameObject[] tankIcons = new GameObject[3];

    public void SetCount(int count)
    {
        int visible = Mathf.Max(count, 0);

        for (int i = 0; i < tankIcons.Length; i++)
        {
            if (tankIcons[i] != null)
            {
                tankIcons[i].SetActive(i < visible);
            }
        }
    }
}
