import type { Recipe, ScheduledMeal } from "@/types/meal-planner";
import { Dispatch, SetStateAction } from "react";
import RecipeCard from "./RecipeCard";

interface RecipeGalleryProps {
  recipes: Recipe[];
  setRecipes: Dispatch<SetStateAction<Recipe[]>>;
  scheduledMeals: ScheduledMeal[];
  setScheduledMeals: Dispatch<SetStateAction<ScheduledMeal[]>>;
}

export default function RecipeGallery({
  recipes,
  setRecipes,
  scheduledMeals,
  setScheduledMeals,
}: RecipeGalleryProps) {
  const isSelected = (recipeId: string) =>
    scheduledMeals.some((m) => m.recipeId === recipeId);

  // Handler: select/deselect recipe (up to 7)
  const handleSelect = (recipeId: string) => {
    if (isSelected(recipeId)) {
      setScheduledMeals(scheduledMeals.filter((m) => m.recipeId !== recipeId));
    } else if (scheduledMeals.length < 7) {
      setScheduledMeals([
        ...scheduledMeals,
        { day: scheduledMeals.length as any, recipeId },
      ]);
    }
  };

  return (
    <div className="grid gap-6 grid-cols-[repeat(auto-fit,minmax(340px,1fr))] justify-center">
      {recipes.map((recipe) => (
        <RecipeCard
          key={recipe.id}
          recipe={recipe}
          selected={isSelected(recipe.id)}
          onSelect={() => handleSelect(recipe.id)}
        />
      ))}
    </div>
  );
}
