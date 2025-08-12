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
  day: DayOfWeek;
  recipeId: string;
}

export interface ScheduleRequest {
  meals: ScheduledMeal[];
}
