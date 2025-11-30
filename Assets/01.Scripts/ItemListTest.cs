using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class MyItem
{
    public string name;
    public int value;
}

public class ItemListTest : MonoBehaviour   
{
    public MyItem[] items;
}