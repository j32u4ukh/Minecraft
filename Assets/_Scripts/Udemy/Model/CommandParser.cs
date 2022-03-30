using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandParser
{
    /// <summary>
    /// 指令應以 / 為開頭
    /// </summary>
    /// <param name="input">對話框輸入的內容</param>
    /// <returns>是否為指令</returns>
    public static bool isCommand(string input)
    {
        return input.StartsWith("/");
    }
}
