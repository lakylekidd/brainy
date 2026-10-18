import { useEffect, useState } from "react";

export default function Today() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  const time = new Intl.DateTimeFormat(undefined, { hour: "2-digit", minute: "2-digit", hourCycle: "h23" }).format(now);
  const seconds = String(now.getSeconds()).padStart(2, "0");
  const date = new Intl.DateTimeFormat(undefined, { weekday: "short", day: "numeric", month: "short", year: "numeric" }).format(now);
  return <section className="today-widget" aria-label="Local date and time">
    <h2>Today</h2><p className="today-date">{date}</p>
    <div className="today-clock">
      <span className="clock-face" aria-hidden="true">
        <i style={{ transform: `rotate(${now.getHours() * 30 + now.getMinutes() / 2}deg)` }} />
        <i className="clock-minute" style={{ transform: `rotate(${now.getMinutes() * 6}deg)` }} />
      </span>
      <div><time dateTime={now.toISOString()}>{time}<small>:{seconds}</small></time><p>Local / {Intl.DateTimeFormat().resolvedOptions().timeZone}</p></div>
    </div>
  </section>;
}
