using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Coordinates : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text coordinatesText; // 如果挂在 TextMeshPro 对象上，通常会自动获取
    [SerializeField] private Transform playerTransform; // 可在 Inspector 指定 Player Transform

    [Header("Display")]
    [SerializeField] private string format = "X: {0:0.00}   Y: {1:0.00}";

    private void Awake()
    {
        if (coordinatesText == null)
            coordinatesText = GetComponent<TMP_Text>();

        if (playerTransform == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) playerTransform = go.transform;
            else
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }
    }

    private void Update()
    {
        if (coordinatesText == null || playerTransform == null) return;

        Vector2 pos = playerTransform.position;
        coordinatesText.text = string.Format(format, pos.x, -pos.y);
    }
}
