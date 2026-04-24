export interface Tag {
  key: string;
  label: string;
  color: string;       // clase Tailwind bg
  textColor: string;   // clase Tailwind text
}

export const TASK_TAGS: Tag[] = [
  // Tipo de trabajo
  { key: 'bug',            label: 'Bug',             color: 'bg-red-100',    textColor: 'text-red-700'    },
  { key: 'feature',        label: 'Feature',         color: 'bg-blue-100',   textColor: 'text-blue-700'   },
  { key: 'improvement',    label: 'Mejora',          color: 'bg-indigo-100', textColor: 'text-indigo-700' },
  { key: 'hotfix',         label: 'Hotfix',          color: 'bg-orange-100', textColor: 'text-orange-700' },
  { key: 'refactor',       label: 'Refactor',        color: 'bg-purple-100', textColor: 'text-purple-700' },
  { key: 'docs',           label: 'Documentación',   color: 'bg-gray-100',   textColor: 'text-gray-700'   },
  { key: 'test',           label: 'Testing',         color: 'bg-teal-100',   textColor: 'text-teal-700'   },
  { key: 'chore',          label: 'Mantenimiento',   color: 'bg-slate-100',  textColor: 'text-slate-700'  },
  // Área funcional
  { key: 'frontend',       label: 'Frontend',        color: 'bg-cyan-100',   textColor: 'text-cyan-700'   },
  { key: 'backend',        label: 'Backend',         color: 'bg-green-100',  textColor: 'text-green-700'  },
  { key: 'design',         label: 'Diseño',          color: 'bg-pink-100',   textColor: 'text-pink-700'   },
  { key: 'devops',         label: 'DevOps',          color: 'bg-yellow-100', textColor: 'text-yellow-700' },
  { key: 'database',       label: 'Base de datos',   color: 'bg-amber-100',  textColor: 'text-amber-700'  },
  { key: 'api',            label: 'API',             color: 'bg-violet-100', textColor: 'text-violet-700' },
  { key: 'security',       label: 'Seguridad',       color: 'bg-red-100',    textColor: 'text-red-800'    },
  { key: 'mobile',         label: 'Mobile',          color: 'bg-sky-100',    textColor: 'text-sky-700'    },
  // Estado de proceso
  { key: 'blocked',        label: 'Bloqueado',       color: 'bg-red-200',    textColor: 'text-red-800'    },
  { key: 'needs-review',   label: 'Necesita revisión', color: 'bg-yellow-100', textColor: 'text-yellow-800' },
  { key: 'ready-to-deploy',label: 'Listo para deploy', color: 'bg-green-100', textColor: 'text-green-800' },
  { key: 'in-qa',          label: 'En QA',           color: 'bg-teal-100',   textColor: 'text-teal-800'   },
  // Impacto
  { key: 'critical',       label: 'Crítico',         color: 'bg-red-100',    textColor: 'text-red-900'    },
  { key: 'performance',    label: 'Performance',     color: 'bg-orange-100', textColor: 'text-orange-800' },
  { key: 'accessibility',  label: 'Accesibilidad',   color: 'bg-blue-100',   textColor: 'text-blue-800'   },
  { key: 'ux',             label: 'UX',              color: 'bg-pink-100',   textColor: 'text-pink-800'   },
];

export const TICKET_TAGS: Tag[] = [
  { key: 'billing',          label: 'Facturación',       color: 'bg-green-100',  textColor: 'text-green-700'  },
  { key: 'technical',        label: 'Técnico',           color: 'bg-blue-100',   textColor: 'text-blue-700'   },
  { key: 'onboarding',       label: 'Onboarding',        color: 'bg-purple-100', textColor: 'text-purple-700' },
  { key: 'data-loss',        label: 'Pérdida de datos',  color: 'bg-red-100',    textColor: 'text-red-700'    },
  { key: 'integration',      label: 'Integración',       color: 'bg-indigo-100', textColor: 'text-indigo-700' },
  { key: 'feature-request',  label: 'Solicitud feature', color: 'bg-cyan-100',   textColor: 'text-cyan-700'   },
  { key: 'bug',              label: 'Bug',               color: 'bg-red-100',    textColor: 'text-red-700'    },
  { key: 'performance',      label: 'Performance',       color: 'bg-orange-100', textColor: 'text-orange-700' },
  { key: 'security',         label: 'Seguridad',         color: 'bg-red-200',    textColor: 'text-red-800'    },
  { key: 'ui',               label: 'Interfaz',          color: 'bg-pink-100',   textColor: 'text-pink-700'   },
  { key: 'account',          label: 'Cuenta',            color: 'bg-slate-100',  textColor: 'text-slate-700'  },
  { key: 'urgent',           label: 'Urgente',           color: 'bg-red-100',    textColor: 'text-red-900'    },
  { key: 'waiting-client',   label: 'Esperando cliente', color: 'bg-yellow-100', textColor: 'text-yellow-800' },
  { key: 'escalated',        label: 'Escalado',          color: 'bg-orange-200', textColor: 'text-orange-900' },
];
