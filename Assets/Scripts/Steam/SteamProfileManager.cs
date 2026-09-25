#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public static class SteamProfileManager
{
    public static event Action<ulong> AvatarUpdated;

    static readonly Dictionary<ulong, Sprite> spriteCache = new Dictionary<ulong, Sprite>();

#if !DISABLESTEAMWORKS
    static Callback<AvatarImageLoaded_t> avatarImageLoaded;
    static Callback<PersonaStateChange_t> personaStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlayMode()
    {
        avatarImageLoaded = null;
        personaStateChanged = null;
        spriteCache.Clear();
        AvatarUpdated = null;
    }

    static void EnsureCallbacks()
    {
        if (avatarImageLoaded != null || !SteamManager.Initialized)
            return;

        avatarImageLoaded = Callback<AvatarImageLoaded_t>.Create(data => InvalidateAvatar(data.m_steamID.m_SteamID));
        personaStateChanged = Callback<PersonaStateChange_t>.Create(data =>
        {
            if ((data.m_nChangeFlags & EPersonaChange.k_EPersonaChangeAvatar) != 0)
                InvalidateAvatar(data.m_ulSteamID);
        });
    }

    static void InvalidateAvatar(ulong steamId)
    {
        spriteCache.Remove(steamId);
        AvatarUpdated?.Invoke(steamId);
    }

    public static ulong GetLocalSteamId()
    {
        return SteamManager.Initialized ? SteamUser.GetSteamID().m_SteamID : 0;
    }

    public static string GetLocalPersonaName()
    {
        return SteamManager.Initialized ? SteamFriends.GetPersonaName() : string.Empty;
    }

    public static Texture2D GetMediumAvatarTexture(CSteamID steamId)
    {
        if (!SteamManager.Initialized)
            return null;

        EnsureCallbacks();

        int imageId = SteamFriends.GetMediumFriendAvatar(steamId);
        if (imageId <= 0)
        {
            SteamFriends.RequestUserInformation(steamId, false);
            return null;
        }

        if (!SteamUtils.GetImageSize(imageId, out uint width, out uint height) || width == 0 || height == 0)
            return null;

        byte[] data = new byte[width * height * 4];
        if (!SteamUtils.GetImageRGBA(imageId, data, data.Length))
            return null;

        FlipRowsVertically(data, (int)width, (int)height);

        Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        texture.LoadRawTextureData(data);
        texture.Apply();
        return texture;
    }

    public static Sprite GetAvatarSprite(ulong steamId)
    {
        if (steamId == 0)
            return null;

        if (spriteCache.TryGetValue(steamId, out Sprite cached) && cached != null)
            return cached;

        Texture2D texture = GetMediumAvatarTexture(new CSteamID(steamId));
        if (texture == null)
            return null;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        spriteCache[steamId] = sprite;
        return sprite;
    }

    static void FlipRowsVertically(byte[] data, int width, int height)
    {
        int stride = width * 4;
        byte[] row = new byte[stride];
        for (int top = 0, bottom = height - 1; top < bottom; top++, bottom--)
        {
            Buffer.BlockCopy(data, top * stride, row, 0, stride);
            Buffer.BlockCopy(data, bottom * stride, data, top * stride, stride);
            Buffer.BlockCopy(row, 0, data, bottom * stride, stride);
        }
    }
#else
    public static ulong GetLocalSteamId()
    {
        return 0;
    }

    public static string GetLocalPersonaName()
    {
        return string.Empty;
    }

    public static Sprite GetAvatarSprite(ulong steamId)
    {
        return null;
    }
#endif
}
