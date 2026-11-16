using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  ColorPaletteDatabase  –  ScriptableObject
//
//  HOW TO CREATE:
//  Right-click in the Project window → Create → WaterPuzzle → Color Palette Database
//
//  One entry per color-palette card shown in the Theme UI.
//  Each entry stores:
//    • displayName  – shown in Editor only
//    • previewSprite – the small bottle thumbnail shown on the card
//    • colors[]     – exactly 12 Color values (indices 0-11)
//                     these map 1-to-1 to colorsIndex[] in every BottleController
// ============================================================

[CreateAssetMenu(
    menuName = "WaterPuzzle/Color Palette Database",
    fileName = "ColorPaletteDatabase")]
public class ColorPaletteDatabase : ScriptableObject
{
    [System.Serializable]
    public class PaletteEntry
    {
        public string displayName = "Palette";
        public Sprite  previewSprite;          // shown on the UI card
        [ColorUsage(false, false)]
        public Color[] colors = new Color[12]; // must match normalColorsPallet size
    }

    public List<PaletteEntry> palettes = new List<PaletteEntry>();

    // ── helpers ──────────────────────────────────────────────

    public int Count => palettes.Count;

    /// <summary>Returns the Color for a given palette index and color slot.</summary>
    public Color GetColor(int paletteIndex, int colorSlot)
    {
        if (paletteIndex < 0 || paletteIndex >= palettes.Count)
        {
            Debug.LogWarning($"[ColorPaletteDatabase] paletteIndex {paletteIndex} out of range.");
            return Color.white;
        }

        var pal = palettes[paletteIndex];

        if (colorSlot < 0 || colorSlot >= pal.colors.Length)
        {
            Debug.LogWarning($"[ColorPaletteDatabase] colorSlot {colorSlot} out of range for palette '{pal.displayName}'.");
            return Color.white;
        }

        return pal.colors[colorSlot];
    }

    public Sprite GetPreviewSprite(int paletteIndex)
    {
        if (paletteIndex < 0 || paletteIndex >= palettes.Count) return null;
        return palettes[paletteIndex].previewSprite;
    }
}
