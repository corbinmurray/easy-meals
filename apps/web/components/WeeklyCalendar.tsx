"use client";
import { Button } from "@workspace/ui/components/button";
import { Card } from "@workspace/ui/components/card";
import { ArrowLeft, ArrowRight, X } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { Dispatch, SetStateAction, useState } from "react";
import type { DayOfWeek, Recipe, ScheduledMeal } from "../types/meal-planner";

interface WeeklyCalendarProps {
  scheduledMeals: ScheduledMeal[];
  setScheduledMeals: Dispatch<SetStateAction<ScheduledMeal[]>>;
  recipes: Recipe[];
}

const DAYS = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

function getStartOfWeek(date: Date): Date {
  const d = new Date(date);
  d.setDate(d.getDate() - d.getDay());
  d.setHours(0, 0, 0, 0);
  return d;
}

export default function WeeklyCalendar({
  scheduledMeals,
  setScheduledMeals,
  recipes,
}: WeeklyCalendarProps) {
  const [weekStart, setWeekStart] = useState<Date>(getStartOfWeek(new Date()));

  // Navigation bounds: ±1 year from today
  const today = new Date();
  const minDate = getStartOfWeek(
    new Date(today.getFullYear() - 1, today.getMonth(), today.getDate())
  );
  const maxDate = getStartOfWeek(
    new Date(today.getFullYear() + 1, today.getMonth(), today.getDate())
  );

  function changeWeek(offset: number) {
    setWeekStart((prev) => {
      const next = new Date(prev);
      next.setDate(next.getDate() + offset * 7);
      if (next < minDate) return minDate;
      if (next > maxDate) return maxDate;
      return next;
    });
  }

  // Get all days in the current week
  const days = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(weekStart);
    d.setDate(weekStart.getDate() + i);
    return d;
  });

  // Helper: get recipes for a day
  function getRecipesForDay(dayIdx: number) {
    const meals = scheduledMeals.filter((m) => m.day === dayIdx);
    return meals
      .map((meal) => recipes.find((r) => r.id === meal.recipeId))
      .filter(Boolean) as Recipe[];
  }

  // Remove recipe from a day
  function handleRemove(dayIdx: number, recipeId: string) {
    setScheduledMeals((prev) =>
      prev.filter((m) => !(m.day === dayIdx && m.recipeId === recipeId))
    );
  }

  // Move recipe to another day
  function handleMoveRecipe(
    currentDay: number,
    recipeId: string,
    newDay: number
  ) {
    setScheduledMeals((prev) =>
      prev.map((m) =>
        m.day === currentDay && m.recipeId === recipeId
          ? { ...m, day: newDay as DayOfWeek }
          : m
      )
    );
  }

  // Responsive grid: horizontal scroll on mobile, adaptive columns
  // Animate week transitions: slide left/right based on navigation
  const [direction, setDirection] = useState<1 | -1>(1);

  function handleWeekNav(offset: number) {
    setDirection(offset > 0 ? 1 : -1);
    changeWeek(offset);
  }

  return (
    <section className="w-full max-w-5xl mx-auto">
      <header className="flex items-center justify-between mb-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => handleWeekNav(-1)}
          disabled={weekStart <= minDate}
          aria-label="Previous week"
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <h2 className="text-lg font-semibold">
          {days[0]?.toLocaleDateString(undefined, {
            month: "short",
            day: "numeric",
          }) ?? ""}
          {" - "}
          {days[6]?.toLocaleDateString(undefined, {
            month: "short",
            day: "numeric",
            year: "numeric",
          }) ?? ""}
        </h2>
        <Button
          variant="ghost"
          size="icon"
          onClick={() => handleWeekNav(1)}
          disabled={weekStart >= maxDate}
          aria-label="Next week"
        >
          <ArrowRight className="w-5 h-5" />
        </Button>
      </header>
      <div className="overflow-x-auto pb-2">
        <AnimatePresence initial={false} custom={direction} mode="wait">
          <motion.div
            key={weekStart.toISOString()}
            custom={direction}
            initial={{ x: direction * 80, opacity: 0 }}
            animate={{ x: 0, opacity: 1 }}
            exit={{ x: -direction * 80, opacity: 0 }}
            transition={{
              type: "spring",
              stiffness: 300,
              damping: 30,
              duration: 0.35,
            }}
            className="grid min-w-[420px] grid-cols-1 xs:grid-cols-2 sm:grid-cols-3 md:grid-cols-5 lg:grid-cols-7 gap-2"
            role="grid"
            aria-label="Weekly meal calendar"
          >
            {days.map((date, idx) => (
              <Card
                key={idx}
                className="flex flex-col min-h-[140px] p-2 bg-base-100"
              >
                <div className="flex items-center justify-between mb-2">
                  <span className="font-medium text-sm">
                    {DAYS[date.getDay()]}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {date.getDate()}
                  </span>
                </div>
                <ul className="flex flex-col gap-1 flex-1">
                  <AnimatePresence initial={false}>
                    {getRecipesForDay(date.getDay()).map((recipe) => (
                      <motion.li
                        key={recipe.id}
                        initial={{ opacity: 0, y: 10 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, y: -10 }}
                        transition={{ duration: 0.2 }}
                        className="relative group"
                      >
                        <div className="flex items-center gap-2 bg-base-200 rounded px-2 py-1">
                          <img
                            src={recipe.image}
                            alt={recipe.name}
                            className="w-8 h-8 rounded object-cover border"
                          />
                          <span className="truncate flex-1 text-sm">
                            {recipe.name}
                          </span>
                          {/* Move to day dropdown */}
                          <div className="relative">
                            <select
                              className="appearance-none bg-transparent text-xs px-2 py-1 rounded border border-transparent hover:border-primary focus:border-primary focus:outline-none cursor-pointer"
                              aria-label={`Move ${recipe.name} to another day`}
                              value={date.getDay()}
                              onChange={(e) =>
                                handleMoveRecipe(
                                  date.getDay(),
                                  recipe.id,
                                  Number(e.target.value)
                                )
                              }
                            >
                              {DAYS.map((d, i) => (
                                <option
                                  key={i}
                                  value={i}
                                  disabled={i === date.getDay()}
                                >
                                  {d}
                                </option>
                              ))}
                            </select>
                          </div>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="opacity-60 group-hover:opacity-100 transition"
                            aria-label={`Remove ${recipe.name} from ${DAYS[date.getDay()]}`}
                            onClick={() =>
                              handleRemove(date.getDay(), recipe.id)
                            }
                          >
                            <X className="w-4 h-4" />
                          </Button>
                        </div>
                      </motion.li>
                    ))}
                  </AnimatePresence>
                </ul>
              </Card>
            ))}
          </motion.div>
        </AnimatePresence>
      </div>
    </section>
  );
}
