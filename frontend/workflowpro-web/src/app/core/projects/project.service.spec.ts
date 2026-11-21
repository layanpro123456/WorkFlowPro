import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProjectService } from './project.service';
import { PagedResult, ProjectDetail, ProjectSummary } from './project.models';

describe('ProjectService', () => {
  let service: ProjectService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(ProjectService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('list should send correct query parameters', () => {
    const mockResult: PagedResult<ProjectSummary> = {
      items: [{
        id: '123',
        projectCode: 'TEST-1',
        name: 'Test Project',
        status: 'Active',
        memberCount: 2,
        createdAtUtc: '2026-11-21'
      }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    };

    service.list('Active', 'query', 1, 10).subscribe((res) => {
      expect(res).toEqual(mockResult);
    });

    const req = httpTesting.expectOne((r) =>
      r.url === '/api/v1/projects' &&
      r.params.get('status') === 'Active' &&
      r.params.get('search') === 'query' &&
      r.params.get('page') === '1' &&
      r.params.get('pageSize') === '10'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('create should post payload and return summary', () => {
    const payload = { projectCode: 'NEW-1', name: 'New Project' };
    const mockSummary: ProjectSummary = {
      id: '999',
      projectCode: 'NEW-1',
      name: 'New Project',
      status: 'Active',
      memberCount: 1,
      createdAtUtc: '2026-11-21'
    };

    service.create(payload).subscribe((res) => {
      expect(res).toEqual(mockSummary);
    });

    const req = httpTesting.expectOne('/api/v1/projects');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(mockSummary);
  });

  it('getById should fetch project detail', () => {
    const mockDetail: ProjectDetail = {
      id: '123',
      projectCode: 'CODE',
      name: 'Name',
      status: 'Active',
      createdById: 'user1',
      createdByName: 'Admin',
      createdAtUtc: '2026-11-21',
      members: []
    };

    service.getById('123').subscribe((res) => {
      expect(res).toEqual(mockDetail);
    });

    const req = httpTesting.expectOne('/api/v1/projects/123');
    expect(req.request.method).toBe('GET');
    req.flush(mockDetail);
  });
});
