"use client";

import { useEffect, useState } from "react";

interface WeatherForecast {
  date: Date;
  temperatureC: number;
  description: string;
}

export default function Page() {
  const [weatherArray, setWeatherArray] = useState<WeatherForecast[]>([]);

  useEffect(() => {
    const fetchWeather = async () => {
      console.log("Fetching weather data...");
      const weatherArray = await fetch("/api/weatherforecast").then((res) =>
        res.json()
      );
      // Convert date strings to Date objects
      setWeatherArray(
        weatherArray.map((w: any) => ({
          ...w,
          date: new Date(w.date),
        }))
      );
    };

    fetchWeather();
  }, []);

  return (
    <main>
      <h1>{weatherArray[0]?.description}</h1>
      <p>{weatherArray[0]?.temperatureC}°C</p>
      <p>{weatherArray[0]?.date.toLocaleDateString()}</p>
    </main>
  );
}
