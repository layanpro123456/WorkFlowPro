import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SprintService } from './sprint.service';

describe('SprintService', () => {
  let service: SprintService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SprintService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists project sprints and fetches velocity', () => {
    service.listProjectSprints('p-1').subscribe(sprints => {
      expect(sprints.length).toBe(1);
      expect(sprints[0].status).toBe('Active');
    });

    const req = httpMock.expectOne('/api/v1/projects/p-1/sprints');
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        id: 'sp-1',
        projectId: 'p-1',
        name: 'Sprint 1',
        status: 'Active',
        totalTasks: 4,
        completedTasks: 1,
        totalPoints: 13,
        completedPoints: 5,
        createdAtUtc: '2026-11-28T00:00:00Z'
      }
    ]);

    service.getVelocity('p-1').subscribe(vel => {
      expect(vel.averageCompletedPoints).toBe(8);
    });
    const velReq = httpMock.expectOne('/api/v1/projects/p-1/velocity');
    expect(velReq.request.method).toBe('GET');
    velReq.flush({ projectId: 'p-1', averageCompletedPoints: 8, sprints: [] });
  });

  it('starts and completes a sprint', () => {
    service.startSprint('sp-1', { startDateUtc: '2026-11-28T00:00:00Z', endDateUtc: '2026-12-12T00:00:00Z' }).subscribe();
    const startReq = httpMock.expectOne('/api/v1/sprints/sp-1/start');
    expect(startReq.request.method).toBe('POST');
    startReq.flush({});

    service.completeSprint('sp-1', { moveIncompleteToSprintId: null }).subscribe();
    const compReq = httpMock.expectOne('/api/v1/sprints/sp-1/complete');
    expect(compReq.request.method).toBe('POST');
    compReq.flush({});
  });
});
