"use client";

import type { Recipe } from "@/types/meal-planner";
import { Badge } from "@workspace/ui/components/badge";
import { Button } from "@workspace/ui/components/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@workspace/ui/components/card";
import { Trash2, X } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";

interface SelectedRecipesListProps {
  selectedRecipes: Recipe[];
  maxRecipes: number;
  onRemoveRecipe: (recipeId: string) => void;
  onClearAll: () => void;
}

export default function SelectedRecipesList({
  selectedRecipes,
  maxRecipes,
  onRemoveRecipe,
  onClearAll,
}: SelectedRecipesListProps) {
  const selectedCount = selectedRecipes.length;
  const hasSelections = selectedCount > 0;

  return (
    <motion.section
      className="w-full"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, delay: 0.1 }}
    >
      <Card className="bg-gradient-to-r from-primary/5 to-accent/5 border-primary/20">
        <CardHeader className="pb-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <CardTitle className="text-xl">Selected Recipes</CardTitle>
              <Badge
                variant={selectedCount === maxRecipes ? "default" : "secondary"}
              >
                {selectedCount} of {maxRecipes}
              </Badge>
            </div>
            {hasSelections && (
              <Button
                variant="ghost"
                size="sm"
                onClick={onClearAll}
                className="text-muted-foreground hover:text-destructive"
                aria-label="Clear all selected recipes"
              >
                <Trash2 className="w-4 h-4 mr-2" />
                Clear All
              </Button>
            )}
          </div>
        </CardHeader>

        <CardContent>
          {!hasSelections ? (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className="text-center py-8 text-muted-foreground"
            >
              <p className="text-lg mb-2">No recipes selected yet</p>
              <p className="text-sm">
                Choose up to {maxRecipes} recipes from the gallery below
              </p>
            </motion.div>
          ) : (
            <div className="grid gap-3 grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              <AnimatePresence>
                {selectedRecipes.map((recipe) => (
                  <motion.div
                    key={recipe.id}
                    layout
                    initial={{ opacity: 0, scale: 0.8 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.8 }}
                    transition={{
                      type: "spring",
                      stiffness: 300,
                      damping: 25,
                      duration: 0.3,
                    }}
                    className="group relative flex items-center gap-3 p-3 bg-background rounded-lg border border-border hover:border-primary/50 transition-colors"
                  >
                    {recipe.image && (
                      <div className="flex-shrink-0">
                        <img
                          src={recipe.image}
                          alt={recipe.name}
                          className="w-12 h-12 rounded-md object-cover border"
                        />
                      </div>
                    )}

                    <div className="flex-1 min-w-0">
                      <h4 className="font-medium text-sm truncate">
                        {recipe.name}
                      </h4>
                      {recipe.tags && recipe.tags.length > 0 && (
                        <div className="flex gap-1 mt-1">
                          {recipe.tags.slice(0, 2).map((tag) => (
                            <Badge
                              key={tag}
                              variant="outline"
                              className="text-xs px-1.5 py-0.5"
                            >
                              {tag}
                            </Badge>
                          ))}
                          {recipe.tags.length > 2 && (
                            <Badge
                              variant="outline"
                              className="text-xs px-1.5 py-0.5"
                            >
                              +{recipe.tags.length - 2}
                            </Badge>
                          )}
                        </div>
                      )}
                    </div>

                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => onRemoveRecipe(recipe.id)}
                      className="opacity-60 group-hover:opacity-100 transition-opacity h-8 w-8 flex-shrink-0"
                      aria-label={`Remove ${recipe.name} from selection`}
                    >
                      <X className="w-4 h-4" />
                    </Button>
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          )}
        </CardContent>
      </Card>
    </motion.section>
  );
}
