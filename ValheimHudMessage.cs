using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakeRenameit;

/// <summary>
/// Valheim 1.0 <c>Character.Message</c> is 5-arg (<c>log</c>). Pfhoenix CI stubs still expose 4-arg.
/// A direct C# call bakes a MethodRef; Mono throws <see cref="MissingMethodException"/> when
/// JIT-compiling <b>any</b> method that contains that token — even a dead branch. Store builds
/// compiled against stubs then abort inventory tooltip / HUD updates every frame.
/// Bind and Invoke at runtime so local (1.0 publicized) and CI (stubs) both work.
/// </summary>
internal static class ValheimHudMessage
{
    private static readonly MethodInfo? Message5 = AccessTools.Method(
        typeof(Character),
        nameof(Character.Message),
        new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite), typeof(bool) });

    private static readonly MethodInfo? Message4 = AccessTools.Method(
        typeof(Character),
        nameof(Character.Message),
        new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite) });

    internal static void Show(Character? character, MessageHud.MessageType type, string? text)
    {
        if (character == null || string.IsNullOrEmpty(text))
            return;

        try
        {
            if (Message5 != null)
            {
                Message5.Invoke(character, new object?[] { type, text, 0, null, false });
                return;
            }

            Message4?.Invoke(character, new object?[] { type, text, 0, null });
        }
        catch (Exception)
        {
            /* never break HUD */
        }
    }
}
