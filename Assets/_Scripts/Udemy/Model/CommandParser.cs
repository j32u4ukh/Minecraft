using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandParser
{
    /// <summary>
    /// ���O���H / ���}�Y
    /// </summary>
    /// <param name="input">��ܮؿ�J�����e</param>
    /// <returns>�O�_�����O</returns>
    public static bool isCommand(string input)
    {
        return input.StartsWith("/");
    }
}
