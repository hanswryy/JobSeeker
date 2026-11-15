using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelNavigator : MonoBehaviour
{
    [SerializeField] GameObject[] appPanels;

    public void ShowOnly(GameObject target)
    {
        if (target == null || System.Array.IndexOf(appPanels, target) < 0)
        {
            Debug.LogError("Panel tujuan belum terdaftar.", this);
            return;
        }

        foreach (GameObject panel in appPanels)
            if (panel != null)
                panel.SetActive(panel == target);
    }
}
