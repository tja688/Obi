using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ItemData
{
    public string name;
    public string describe;
    public Sprite img;
    public ItemData(string name,string describe,Sprite img)
    {
        this.name = name;
        this.describe = describe;
        this.img = img;
    }
}
