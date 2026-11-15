using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelNavigator : MonoBehaviour
{
    [SerializeField] GameObject[] appPanels;
    [SerializeField] GameObject desktopPanel;

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

    public void ShowDesktop()
    {
        if (appPanels == null)
        {
            Debug.LogError("Daftar panel aplikasi belum di-assign.", this);
            return;
        }
        if (desktopPanel == null)
        {
            Debug.LogError("Desktop Panel belum di-assign pada PanelNavigator.", this);
            return;
        }

        foreach (GameObject panel in appPanels)
            if (panel != null)
                panel.SetActive(false);

        desktopPanel.SetActive(true);
    }
}
