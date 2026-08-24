/**
 * Shares the current wallpaper's natural pixel size across the app. The glass
 * cards need it to line their liquid-glass distortion layer up pixel-for-pixel
 * with the wallpaper actually behind them (a plain CSS `cover` can't do that —
 * it would re-crop/zoom the image inside each card).
 */
export interface WallpaperMetrics {
  url: string
  /** naturalWidth of the loaded wallpaper. */
  iw: number
  /** naturalHeight of the loaded wallpaper. */
  ih: number
}

const EVENT = 'glass:wallpaper-metrics'
let current: WallpaperMetrics | null = null

export function setWallpaperMetrics(m: WallpaperMetrics): void {
  current = m
  window.dispatchEvent(new CustomEvent<WallpaperMetrics>(EVENT, { detail: m }))
}

export function getWallpaperMetrics(): WallpaperMetrics | null {
  return current
}

/** Subscribe to wallpaper changes; returns an unsubscribe function. */
export function onWallpaperMetrics(cb: (m: WallpaperMetrics) => void): () => void {
  const handler = (e: Event): void => cb((e as CustomEvent<WallpaperMetrics>).detail)
  window.addEventListener(EVENT, handler)
  return () => window.removeEventListener(EVENT, handler)
}
