import { useEffect, useState } from 'react'
import { GlassCard } from './GlassCard'
import './DateCard.css'

const MONTHS = [
  'JANUARY',
  'FEBRUARY',
  'MARCH',
  'APRIL',
  'MAY',
  'JUNE',
  'JULY',
  'AUGUST',
  'SEPTEMBER',
  'OCTOBER',
  'NOVEMBER',
  'DECEMBER'
] as const

const pad2 = (v: number): string => String(v).padStart(2, '0')

/** Human-readable label, e.g. "26 JULY 2026". */
function formatDate(date: Date): string {
  return `${date.getDate()} ${MONTHS[date.getMonth()]} ${date.getFullYear()}`
}

/** Machine-readable local date for the <time> element, e.g. "2026-07-26". */
function isoDate(date: Date): string {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

/**
 * The date display. Renders into its own GlassCard beneath the clock and
 * refreshes exactly at midnight rather than polling every second.
 */
export function DateCard(): JSX.Element {
  const [now, setNow] = useState<Date>(() => new Date())

  useEffect(() => {
    let timeoutId: number

    const scheduleNextMidnight = (): void => {
      const current = new Date()
      // One second past the next midnight, to be safely on the new day.
      const nextMidnight = new Date(
        current.getFullYear(),
        current.getMonth(),
        current.getDate() + 1,
        0,
        0,
        1,
        0
      )
      timeoutId = window.setTimeout(() => {
        setNow(new Date())
        scheduleNextMidnight()
      }, nextMidnight.getTime() - current.getTime())
    }

    scheduleNextMidnight()
    return () => window.clearTimeout(timeoutId)
  }, [])

  return (
    <GlassCard className="date-card" softDistort>
      <time className="date" dateTime={isoDate(now)}>
        {formatDate(now)}
      </time>
    </GlassCard>
  )
}
