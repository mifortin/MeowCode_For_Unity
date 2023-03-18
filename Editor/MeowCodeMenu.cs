//  This Source Code Form is subject to the terms of the Mozilla Public
//  License, v. 2.0. If a copy of the MPL was not distributed with this
//  file, You can obtain one at https://mozilla.org/MPL/2.0/. 

using UnityEditor;
using UnityEngine;

public class MeowCodeMenu : MonoBehaviour
{
	private static string key = "bMeowCodeEnable";
	
	static public bool IsCodeGenEnable()
	{
		return PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) != 0 ;
	}

	[MenuItem("MeowCode/Enable")]
	static void DoEnable()
	{
		PlayerPrefs.SetInt(key, 1);
	}

	[MenuItem("MeowCode/Disable")]
	static void DoDisable()
	{
		PlayerPrefs.SetInt(key, 0);
	}

	[MenuItem("MeowCode/Enable", true)]
	static bool OnValidateEnable()
	{
		return !IsCodeGenEnable();
	}
	
	[MenuItem("MeowCode/Disable", true)]
	static bool OnValidateDisable()
	{
		return IsCodeGenEnable();
	}
}
