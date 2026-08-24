import { useEffect, useState } from 'react'
import { DEFAULT_SETTINGS } from '@shared/settings'
import { DEFAULT_WALLPAPER_URL, resolveWallpaperUrl } from '../lib/wallpapers'
import { setWallpaperMetrics } from '../lib/wallpaperMetrics'
import './Background.css'

/**
 * The single component responsible for displaying the wallpaper.
 *
 * The saved setting is read synchronously at load (via the preload bridge), so
 * the first render already targets the correct wallpaper — no default-then-swap
 * flash. The image fades up from black once it decodes, so the brief pre-decode
 * moment reads as an intentional dissolve rather than a black flash. A built-in
 * JPG or the user's custom image (served through the glass-asset protocol) both
 * work; the renderer never touches the filesystem.
 */
export function Background(): JSX.Element {
  const initial = window.glass?.initialSettings ?? DEFAULT_SETTINGS
  const [src, setSrc] = useState<string>(() => resolveWallpaperUrl(initial.backgroundImage))
  const [loaded, setLoaded] = useState(false)

  // Publish the current wallpaper URL so the glass cards can sample it for their
  // liquid-glass distortion layer (see .glass-card__distort).
  useEffect(() => {
    document.documentElement.style.setProperty('--wallpaper-url', `url("${src}")`)
  }, [src])

  useEffect(() => {
    let active = true
    const apply = (backgroundImage: string): void => {
      if (active) setSrc(resolveWallpaperUrl(backgroundImage))
    }

    // Correct against the authoritative value (covers a rare sync-read miss),
    // then track live changes from the Settings window.
    window.glass
      .getSettings()
      .then((settings) => apply(settings.backgroundImage))
      .catch(() => {
        /* keep the synchronous initial value */
      })
    const off = window.glass.onSettingsChanged((settings) => apply(settings.backgroundImage))
    return () => {
      active = false
      off()
    }
  }, [])

  const handleError = (): void => {
    if (src !== DEFAULT_WALLPAPER_URL) setSrc(DEFAULT_WALLPAPER_URL)
  }

  return (
    <div className="background">
      <img
        className={loaded ? 'background__image background__image--loaded' : 'background__image'}
        src={src}
        alt=""
        draggable={false}
        onLoad={(e) => {
          setLoaded(true)
          const img = e.currentTarget
          setWallpaperMetrics({ url: src, iw: img.naturalWidth, ih: img.naturalHeight })
        }}
        onError={handleError}
      />
    </div>
  )
}
