import { Dispatch, SetStateAction } from "react";
import type { Recipe, ScheduledMeal } from "../types/meal-planner";

interface WeeklyCalendarProps {
  scheduledMeals: ScheduledMeal[];
  setScheduledMeals: Dispatch<SetStateAction<ScheduledMeal[]>>;
  recipes: Recipe[];
}

export default function WeeklyCalendar({
  scheduledMeals,
  setScheduledMeals,
  recipes,
}: WeeklyCalendarProps) {
  // TODO: Implement calendar UI and meal assignment logic
  return <div>Weekly Calendar</div>;
}
