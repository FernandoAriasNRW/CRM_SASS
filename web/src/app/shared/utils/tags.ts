export interface Tag {
  key: string;
  label: string;
  color: string;       // clase Tailwind bg
  textColor: string;   // clase Tailwind text
}

export const TASK_TAGS: Tag[] = [
  // Tipo de trabajo
  { key: 'bug',            label: 'Bug',             color: 'bg-red-100 dark:bg-red-950/40',    textColor: 'text-red-700 dark:text-red-300'    },
  { key: 'feature',        label: 'Feature',         color: 'bg-blue-100 dark:bg-blue-950/40',   textColor: 'text-blue-700 dark:text-blue-300'   },
  { key: 'improvement',    label: 'Mejora',          color: 'bg-indigo-100 dark:bg-indigo-950/40', textColor: 'text-indigo-700 dark:text-indigo-300' },
  { key: 'hotfix',         label: 'Hotfix',          color: 'bg-orange-100 dark:bg-orange-950/40', textColor: 'text-orange-700 dark:text-orange-300' },
  { key: 'refactor',       label: 'Refactor',        color: 'bg-purple-100 dark:bg-purple-950/40', textColor: 'text-purple-700 dark:text-purple-300' },
  { key: 'docs',           label: 'Documentación',   color: 'bg-gray-100 dark:bg-gray-950/40',   textColor: 'text-gray-700 dark:text-gray-300'   },
  { key: 'test',           label: 'Testing',         color: 'bg-teal-100 dark:bg-teal-950/40',   textColor: 'text-teal-700 dark:text-teal-300'   },
  { key: 'chore',          label: 'Mantenimiento',   color: 'bg-slate-100 dark:bg-slate-950/40',  textColor: 'text-slate-700 dark:text-slate-300'  },
  // Área funcional
  { key: 'frontend',       label: 'Frontend',        color: 'bg-cyan-100 dark:bg-cyan-950/40',   textColor: 'text-cyan-700 dark:text-cyan-300'   },
  { key: 'backend',        label: 'Backend',         color: 'bg-green-100 dark:bg-green-950/40',  textColor: 'text-green-700 dark:text-green-300'  },
  { key: 'design',         label: 'Diseño',          color: 'bg-pink-100 dark:bg-pink-950/40',   textColor: 'text-pink-700 dark:text-pink-300'   },
  { key: 'devops',         label: 'DevOps',          color: 'bg-yellow-100 dark:bg-yellow-950/40', textColor: 'text-yellow-700 dark:text-yellow-300' },
  { key: 'database',       label: 'Base de datos',   color: 'bg-amber-100 dark:bg-amber-950/40',  textColor: 'text-amber-700 dark:text-amber-300'  },
  { key: 'api',            label: 'API',             color: 'bg-violet-100 dark:bg-violet-950/40', textColor: 'text-violet-700 dark:text-violet-300' },
  { key: 'security',       label: 'Seguridad',       color: 'bg-red-100 dark:bg-red-950/40',    textColor: 'text-red-800 dark:text-red-300'    },
  { key: 'mobile',         label: 'Mobile',          color: 'bg-sky-100 dark:bg-sky-950/40',    textColor: 'text-sky-700 dark:text-sky-300'    },
  // Estado de proceso
  { key: 'blocked',        label: 'Bloqueado',       color: 'bg-red-200 dark:bg-red-900/50',    textColor: 'text-red-800 dark:text-red-300'    },
  { key: 'needs-review',   label: 'Necesita revisión', color: 'bg-yellow-100 dark:bg-yellow-950/40', textColor: 'text-yellow-800 dark:text-yellow-300' },
  { key: 'ready-to-deploy',label: 'Listo para deploy', color: 'bg-green-100 dark:bg-green-950/40', textColor: 'text-green-800 dark:text-green-300' },
  { key: 'in-qa',          label: 'En QA',           color: 'bg-teal-100 dark:bg-teal-950/40',   textColor: 'text-teal-800 dark:text-teal-300'   },
  // Impacto
  { key: 'critical',       label: 'Crítico',         color: 'bg-red-100 dark:bg-red-950/40',    textColor: 'text-red-900 dark:text-red-200'    },
  { key: 'performance',    label: 'Performance',     color: 'bg-orange-100 dark:bg-orange-950/40', textColor: 'text-orange-800 dark:text-orange-300' },
  { key: 'accessibility',  label: 'Accesibilidad',   color: 'bg-blue-100 dark:bg-blue-950/40',   textColor: 'text-blue-800 dark:text-blue-300'   },
  { key: 'ux',             label: 'UX',              color: 'bg-pink-100 dark:bg-pink-950/40',   textColor: 'text-pink-800 dark:text-pink-300'   },
];

export const TICKET_TAGS: Tag[] = [
  { key: 'billing',          label: 'Facturación',       color: 'bg-green-100 dark:bg-green-950/40',  textColor: 'text-green-700 dark:text-green-300'  },
  { key: 'technical',        label: 'Técnico',           color: 'bg-blue-100 dark:bg-blue-950/40',   textColor: 'text-blue-700 dark:text-blue-300'   },
  { key: 'onboarding',       label: 'Onboarding',        color: 'bg-purple-100 dark:bg-purple-950/40', textColor: 'text-purple-700 dark:text-purple-300' },
  { key: 'data-loss',        label: 'Pérdida de datos',  color: 'bg-red-100 dark:bg-red-950/40',    textColor: 'text-red-700 dark:text-red-300'    },
  { key: 'integration',      label: 'Integración',       color: 'bg-indigo-100 dark:bg-indigo-950/40', textColor: 'text-indigo-700 dark:text-indigo-300' },
  { key: 'feature-request',  label: 'Solicitud feature', color: 'bg-cyan-100 dark:bg-cyan-950/40',   textColor: 'text-cyan-700 dark:text-cyan-300'   },
  { key: 'bug',              label: 'Bug',               color: 'bg-red-100 dark:bg-red-950/40',    textColor: 'text-red-700 dark:text-red-300'    },
  { key: 'performance',      label: 'Performance',       color: 'bg-orange-100 dark:bg-orange-950/40', textColor: 'text-orange-700 dark:text-orange-300' },
  { key: 'security',         label: 'Seguridad',         color: 'bg-red-200 dark:bg-red-900/50',    textColor: 'text-red-800 dark:text-red-300'    },
  { key: 'ui',               label: 'Interfaz',          color: 'bg-pink-100 dark:bg-pink-950/40',   textColor: 'text-pink-700 dark:text-pink-300'   },
  { key: 'account',          label: 'Cuenta',            color: 'bg-slate-100 dark:bg-slate-950/40',  textColor: 'text-slate-700 dark:text-slate-300'  },
  { key: 'urgent',           label: 'Urgente',           color: 'bg-red-100 dark:bg-red-950/40',    textColor: 'text-red-900 dark:text-red-200'    },
  { key: 'waiting-client',   label: 'Esperando cliente', color: 'bg-yellow-100 dark:bg-yellow-950/40', textColor: 'text-yellow-800 dark:text-yellow-300' },
  { key: 'escalated',        label: 'Escalado',          color: 'bg-orange-200 dark:bg-orange-900/50', textColor: 'text-orange-900 dark:text-orange-200' },
];
