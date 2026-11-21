using System;
using UnityEngine;

public static class CoinManager
{
    private const string CoinsKey = "PlayerCoins";
    private const int DefaultStartingCoins = 200;

    public static event Action<int> OnCoinsChanged;

    public static int Coins
    {
        get
        {
            if (!PlayerPrefs.HasKey(CoinsKey))
            {
                PlayerPrefs.SetInt(CoinsKey, DefaultStartingCoins);
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetInt(CoinsKey, DefaultStartingCoins);
        }
    }

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;

        int updated = Coins + amount;
        PlayerPrefs.SetInt(CoinsKey, updated);
        PlayerPrefs.Save();
        AudioManager.PlayCoin();
        OnCoinsChanged?.Invoke(updated);
    }

    public static bool TrySpendCoins(int cost)
    {
        if (cost <= 0) return true;
        if (Coins < cost) return false;

        int updated = Coins - cost;
        PlayerPrefs.SetInt(CoinsKey, updated);
        PlayerPrefs.Save();
        AudioManager.PlayUIClick();
        OnCoinsChanged?.Invoke(updated);
        return true;
    }
}
