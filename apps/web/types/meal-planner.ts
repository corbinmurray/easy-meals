// types/meal-planner.ts

export interface Recipe {
  id: string;
  name: string;
  image: string;
  description: string;
  tags?: string[];
}

export type DayOfWeek = 0 | 1 | 2 | 3 | 4 | 5 | 6; // 0 = Sunday, 6 = Saturday

export interface ScheduledMeal {
  day?: DayOfWeek; // Optional - user might not specify a day
  recipeId: string;
}

export interface ScheduleRequest {
  meals: ScheduledMeal[];
}

// New types for week-based meal planning
export interface WeeklyMealPlan {
  weekStartDate: string; // ISO date string for the start of the week
  meals: ScheduledMeal[];
}

export interface WeeklyMealPlanState {
  [weekStartDate: string]: ScheduledMeal[];
}
