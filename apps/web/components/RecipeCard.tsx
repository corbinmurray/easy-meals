import type { Recipe } from "@/types/meal-planner";
import { Badge } from "@workspace/ui/components/badge";
import { Button } from "@workspace/ui/components/button";
import {
  CardContent,
  CardDescription,
  CardFooter,
  CardTitle,
} from "@workspace/ui/components/card";
import { CheckCircle2 } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";

interface RecipeCardProps {
  recipe: Recipe;
  selected: boolean;
  onSelect: () => void;
}

export default function RecipeCard({
  recipe,
  selected,
  onSelect,
}: RecipeCardProps) {
  return (
    <motion.div
      whileHover={{
        scale: 1.02,
        transition: {
          type: "spring" as const,
          stiffness: 400,
          damping: 30,
          duration: 0.3,
        },
      }}
      initial={{ scale: 1 }}
    >
      <motion.div
        className={`group relative flex flex-col h-full min-h-[380px] max-h-[420px] p-0 overflow-hidden transition-shadow duration-300 rounded-lg border bg-card text-card-foreground shadow-sm ${
          !selected && "hover:shadow-xl hover:shadow-black/10"
        }`}
        animate={{
          boxShadow: selected
            ? "0 0 0 2px oklch(68.628% .185 148.958), 0 10px 25px -5px oklch(68.628% .185 148.958 / 0.2)"
            : "0 0 0 0px transparent, 0 4px 6px -1px rgb(0 0 0 / 0.1)",
        }}
        transition={{
          type: "spring" as const,
          stiffness: 300,
          damping: 30,
          duration: 0.4,
        }}
      >
        {/* Image section with overlay gradient */}
        <div className="relative h-32 w-full overflow-hidden bg-gradient-to-br from-muted to-muted/50">
          {recipe.image ? (
            <motion.div
              className="absolute inset-0 bg-cover bg-center"
              style={{ backgroundImage: `url(${recipe.image})` }}
              whileHover={{
                scale: 1.05,
                transition: {
                  type: "spring" as const,
                  stiffness: 300,
                  damping: 25,
                  duration: 0.4,
                },
              }}
              initial={{ scale: 1 }}
            />
          ) : (
            <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-primary/10 to-accent/10">
              <div className="text-6xl font-bold text-primary/30">
                {recipe.name.slice(0, 2).toUpperCase()}
              </div>
            </div>
          )}
          <div className="absolute inset-0 bg-gradient-to-t from-black/30 via-transparent to-transparent" />
        </div>

        {/* Content section */}
        <div className="flex flex-col flex-1 p-4">
          {/* Title and tags */}
          <div className="space-y-3">
            <CardTitle className="text-lg font-semibold leading-tight line-clamp-2 group-hover:text-primary transition-colors">
              {recipe.name}
            </CardTitle>

            {/* Tags */}
            {recipe.tags && recipe.tags.length > 0 && (
              <div className="flex flex-wrap gap-1.5 min-h-[24px]">
                {recipe.tags.slice(0, 3).map((tag) => (
                  <Badge
                    key={tag}
                    variant="secondary"
                    className="text-xs px-2 py-0.5 bg-secondary/80 hover:bg-secondary transition-colors"
                  >
                    {tag}
                  </Badge>
                ))}
                {recipe.tags.length > 3 && (
                  <Badge variant="outline" className="text-xs px-2 py-0.5">
                    +{recipe.tags.length - 3}
                  </Badge>
                )}
              </div>
            )}
          </div>

          {/* Description */}
          <CardContent className="flex-1 px-0 pt-3 pb-0">
            <CardDescription className="text-sm text-muted-foreground line-clamp-3 leading-relaxed">
              {recipe.description}
            </CardDescription>
          </CardContent>

          {/* Action button */}
          <CardFooter className="px-0 pt-4 pb-0">
            <motion.div
              className="w-full"
              animate={{
                scale: selected ? 1.02 : 1,
              }}
              transition={{
                type: "spring" as const,
                stiffness: 400,
                damping: 30,
                duration: 0.3,
              }}
            >
              <Button
                variant={selected ? "default" : "outline"}
                onClick={onSelect}
                className={`w-full transition-all duration-300 ${
                  selected
                    ? "bg-primary hover:bg-primary/90 shadow-md"
                    : "hover:cursor-pointer"
                }`}
              >
                <AnimatePresence mode="popLayout">
                  {selected ? (
                    <motion.div
                      key="selected"
                      initial={{ scale: 0, rotate: -180 }}
                      animate={{ scale: 1, rotate: 0 }}
                      exit={{ scale: 0, rotate: 180 }}
                      className="flex items-center justify-center"
                    >
                      <CheckCircle2 className="w-4 h-4" />
                    </motion.div>
                  ) : (
                    <motion.span
                      key="unselected"
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -10 }}
                    >
                      Select Recipe
                    </motion.span>
                  )}
                </AnimatePresence>
              </Button>
            </motion.div>
          </CardFooter>
        </div>
      </motion.div>
    </motion.div>
  );
}
