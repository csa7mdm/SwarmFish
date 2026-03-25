import { SimulationSummary, SimulationState } from '@/types/api';
import { Card, CardContent, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { CalendarIcon, UsersIcon } from 'lucide-react';

interface SimulationCardProps {
  simulation: SimulationSummary;
}

const getStatusColor = (status: SimulationState) => {
  switch (status) {
    case 'Initialising':
      return 'bg-blue-500 hover:bg-blue-600';
    case 'Running':
      return 'bg-green-500 hover:bg-green-600';
    case 'Paused':
      return 'bg-amber-500 hover:bg-amber-600';
    case 'Completed':
      return 'bg-purple-500 hover:bg-purple-600';
    case 'Failed':
      return 'bg-red-500 hover:bg-red-600';
    default:
      return 'bg-gray-500 hover:bg-gray-600';
  }
};

export function SimulationCard({ simulation }: SimulationCardProps) {
  const progressPercent = simulation.totalRounds > 0 
    ? Math.round((simulation.currentRound / simulation.totalRounds) * 100) 
    : 0;

  return (
    <Card className="flex flex-col h-full bg-slate-900 border-slate-800 hover:border-slate-700 transition-colors">
      <CardHeader className="pb-3">
        <div className="flex justify-between items-start mb-2">
          <Badge className={`${getStatusColor(simulation.status)} text-white`}>
            {simulation.status}
          </Badge>
          <span className="text-xs text-slate-400 font-mono" title={simulation.id}>
            {simulation.id.substring(0, 8)}
          </span>
        </div>
        <CardTitle className="text-lg font-medium text-slate-100 line-clamp-2" title={simulation.seedDocumentTitle}>
          {simulation.seedDocumentTitle}
        </CardTitle>
      </CardHeader>
      
      <CardContent className="flex-1 pb-4">
        <div className="flex items-center text-sm text-slate-400 mb-4">
          <UsersIcon className="w-4 h-4 mr-2" />
          {simulation.agentCount.toLocaleString()} Agents
        </div>
        
        <div className="space-y-1.5">
          <div className="flex justify-between text-xs text-slate-400">
            <span>Progress</span>
            <span>{simulation.currentRound} / {simulation.totalRounds}</span>
          </div>
          <Progress value={progressPercent} className="h-2" />
        </div>
      </CardContent>
      
      <CardFooter className="pt-0 border-t border-slate-800 mt-auto pt-4 text-xs text-slate-500 flex items-center">
        <CalendarIcon className="w-3.5 h-3.5 mr-1.5" />
        {new Date(simulation.createdAt).toLocaleString()}
      </CardFooter>
    </Card>
  );
}
