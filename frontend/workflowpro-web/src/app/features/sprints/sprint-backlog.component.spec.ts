import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { SprintBacklogComponent } from './sprint-backlog.component';
import { ProjectService } from '../../core/projects/project.service';
import { SprintService } from '../../core/sprints/sprint.service';
import { TaskService } from '../../core/tasks/task.service';

describe('SprintBacklogComponent', () => {
  let component: SprintBacklogComponent;
  let fixture: ComponentFixture<SprintBacklogComponent>;
  let projectService: ProjectService;
  let sprintService: SprintService;
  let taskService: TaskService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SprintBacklogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'proj-1' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SprintBacklogComponent);
    component = fixture.componentInstance;
    projectService = TestBed.inject(ProjectService);
    sprintService = TestBed.inject(SprintService);
    taskService = TestBed.inject(TaskService);
  });

  it('loads sprints, velocity, burndown, and backlog tasks on init', () => {
    vi.spyOn(projectService, 'getById').mockReturnValue(of({
      id: 'proj-1',
      projectCode: 'JIVA',
      name: 'Jiva Core',
      status: 'Active',
      createdById: 'u-1',
      createdByName: 'Admin',
      createdAtUtc: '2026-11-28',
      members: []
    }));

    vi.spyOn(sprintService, 'listProjectSprints').mockReturnValue(of([{
      id: 'sp-1',
      projectId: 'proj-1',
      name: 'Sprint 1',
      status: 'Active',
      totalTasks: 1,
      completedTasks: 0,
      totalPoints: 5,
      completedPoints: 0,
      createdAtUtc: '2026-11-28'
    }]));

    vi.spyOn(sprintService, 'getBurndown').mockReturnValue(of({
      sprintId: 'sp-1',
      sprintName: 'Sprint 1',
      status: 'Active',
      totalPoints: 5,
      points: [
        { date: '2026-11-28', idealRemainingPoints: 5, actualRemainingPoints: 5, completedPoints: 0, remainingTaskCount: 1 },
        { date: '2026-11-29', idealRemainingPoints: 0, actualRemainingPoints: 5, completedPoints: 0, remainingTaskCount: 1 }
      ]
    }));

    vi.spyOn(sprintService, 'getVelocity').mockReturnValue(of({
      projectId: 'proj-1',
      averageCompletedPoints: 5,
      sprints: []
    }));

    vi.spyOn(taskService, 'listProjectTasks').mockReturnValue(of({
      items: [
        {
          id: 'epic-1',
          projectId: 'proj-1',
          projectCode: 'JIVA',
          projectName: 'Jiva Core',
          taskKey: 'JIVA-1',
          title: 'Agile Epic',
          status: 'InProgress',
          priority: 'High',
          issueType: 'Epic',
          createdById: 'u-1',
          createdByName: 'Admin',
          version: 1,
          commentCount: 0,
          createdAtUtc: '2026-11-28'
        },
        {
          id: 'st-1',
          projectId: 'proj-1',
          projectCode: 'JIVA',
          projectName: 'Jiva Core',
          taskKey: 'JIVA-2',
          title: 'Sprint Story',
          status: 'ToDo',
          priority: 'Normal',
          issueType: 'Story',
          storyPoints: 5,
          sprintId: 'sp-1',
          createdById: 'u-1',
          createdByName: 'Admin',
          version: 1,
          commentCount: 0,
          createdAtUtc: '2026-11-28'
        },
        {
          id: 'bl-1',
          projectId: 'proj-1',
          projectCode: 'JIVA',
          projectName: 'Jiva Core',
          taskKey: 'JIVA-3',
          title: 'Backlog Bug',
          status: 'Backlog',
          priority: 'High',
          issueType: 'Bug',
          storyPoints: 3,
          createdById: 'u-1',
          createdByName: 'Admin',
          version: 1,
          commentCount: 0,
          createdAtUtc: '2026-11-28'
        }
      ],
      page: 1,
      pageSize: 100,
      totalCount: 3,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.epics().length).toBe(1);
    expect(component.getSprintTasks('sp-1').length).toBe(1);
    expect(component.backlogTasks().length).toBe(1);
    expect(component.backlogPoints()).toBe(3);
    expect(component.idealPolyline().length).toBeGreaterThan(0);
  });
});
