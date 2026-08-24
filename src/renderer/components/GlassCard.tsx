import { useEffect, useRef } from 'react'
import type { HTMLAttributes, ReactNode } from 'react'
import { getWallpaperMetrics, onWallpaperMetrics } from '../lib/wallpaperMetrics'
import './GlassCard.css'

export interface GlassCardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode
  /** Use a gentler distortion tuned for small cards (e.g. the date), where the
   *  full-strength ripple is too large a fraction of the card and looks glitchy. */
  softDistort?: boolean
}

/**
 * A reusable frosted-glass surface. Purely presentational — it holds no clock
 * or app logic and simply wraps its children in a glassmorphism container.
 * This is the foundation every future UI element builds on.
 *
 * Extra props (className, style, etc.) are forwarded to the root element so
 * callers can position or extend the card without touching its visual base.
 */
export function GlassCard({
  children,
  className,
  softDistort = false,
  ...rest
}: GlassCardProps): JSX.Element {
  const distortRef = useRef<HTMLSpanElement>(null)
  const distortFilter = softDistort
    ? 'url(#glass-liquid-soft) blur(3px) saturate(1.4)'
    : 'url(#glass-liquid) blur(5px) saturate(1.5)'

  // Align the distortion layer's wallpaper to the slice actually behind the card,
  // so it reads as looking *through* the glass rather than a re-cropped copy. We
  // size the background to the on-screen (cover) wallpaper and offset it by the
  // card's viewport position. Recomputed on resize, layout change, and wallpaper
  // change.
  useEffect(() => {
    const el = distortRef.current
    if (!el) return

    const align = (): void => {
      const m = getWallpaperMetrics()
      if (!m || !m.iw || !m.ih) return
      const rect = el.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) return

      const vw = window.innerWidth
      const vh = window.innerHeight
      const scale = Math.max(vw / m.iw, vh / m.ih) // object-fit: cover
      const renderedW = m.iw * scale
      const renderedH = m.ih * scale
      const imgLeft = (vw - renderedW) / 2 // wallpaper is centered
      const imgTop = (vh - renderedH) / 2

      el.style.backgroundSize = `${renderedW}px ${renderedH}px`
      el.style.backgroundPosition = `${imgLeft - rect.left}px ${imgTop - rect.top}px`
    }

    align()
    const ro = new ResizeObserver(align)
    ro.observe(el)
    window.addEventListener('resize', align)
    const off = onWallpaperMetrics(align)
    return () => {
      ro.disconnect()
      window.removeEventListener('resize', align)
      off()
    }
  }, [])

  return (
    <div className={className ? `glass-card ${className}` : 'glass-card'} {...rest}>
      {/* Liquid-glass distortion layer: the wallpaper slice behind the card,
          blurred and rippled by the animated #glass-liquid filter. The filter is
          applied inline (not via the external stylesheet) so its url(#...)
          fragment resolves against the document; background size/position are set
          in JS (see the effect above) for pixel alignment. */}
      <span
        ref={distortRef}
        className="glass-card__distort"
        aria-hidden="true"
        style={{ filter: distortFilter }}
      />
      <span className="glass-card__refraction" aria-hidden="true" />
      <div className="glass-card__content">{children}</div>
    </div>
  )
}
