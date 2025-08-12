import type { Recipe, ScheduledMeal } from "../types/meal-planner";

interface SelectedMealsSummaryProps {
  scheduledMeals: ScheduledMeal[];
  recipes: Recipe[];
}

export default function SelectedMealsSummary({
  scheduledMeals,
  recipes,
}: SelectedMealsSummaryProps) {
  // TODO: Implement summary display
  return <div>Selected Meals Summary</div>;
}
